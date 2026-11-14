using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Staff accounts: creating coaches, roles, permission switches, deactivating and password resets
    public class UserServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        private static CreateCoachDto NewCoach(UserRole role = UserRole.PositionCoach, TeamUnit? unit = null, string email = "new.coach@example.com")
        {
            return new CreateCoachDto
            {
                FirstName = "New",
                LastName = "Coach",
                Email = email,
                Role = role,
                CoordinatorUnit = unit
            };
        }

        [Fact]
        public async Task CreateCoach_ReturnsATemporaryPassword_ThatMustBeChangedAtFirstSignIn()
        {
            var headCoach = await _world.AddTeamAsync();

            TemporaryPasswordDto created;
            using (var context = _world.NewContext())
            {
                created = await _world.Users(context).CreateCoachAsync(headCoach, NewCoach(UserRole.Coordinator, TeamUnit.Defence));
            }

            Assert.Equal(12, created.TemporaryPassword.Length);

            using var loginContext = _world.NewContext();
            var login = await _world.Auth(loginContext).LoginAsync(new LoginDto { Email = created.Email, Password = created.TemporaryPassword });

            Assert.NotNull(login);
            Assert.True(login!.MustChangePassword);
            Assert.Equal(UserRole.Coordinator, login.Role);
            Assert.Equal(TeamUnit.Defence, login.CoordinatorUnit);
            Assert.Equal(headCoach.TeamId, login.TeamId);
        }

        [Fact]
        public async Task CreateCoach_NewCoachCannotPublishPlaybookChangesByDefault()
        {
            var headCoach = await _world.AddTeamAsync();

            using var context = _world.NewContext();
            var created = await _world.Users(context).CreateCoachAsync(headCoach, NewCoach());
            var coach = await _world.Users(context).GetByIdAsync(headCoach, created.UserId);

            Assert.False(coach!.CanPublishPlaybook);
            Assert.True(coach.CanEditDepthChart);
            Assert.True(coach.CanRecordAttendance);
        }

        [Fact]
        public async Task CreateCoach_OnlyTheHeadCoachMay()
        {
            var headCoach = await _world.AddTeamAsync();
            var coordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            var player = await _world.AddPlayerAsync(headCoach, "Walker");

            using var context = _world.NewContext();
            await Assert.ThrowsAsync<ForbiddenException>(() => _world.Users(context).CreateCoachAsync(coordinator, NewCoach()));
            await Assert.ThrowsAsync<ForbiddenException>(() => _world.Users(context).CreateCoachAsync(player, NewCoach()));
        }

        [Theory]
        [InlineData(UserRole.HeadCoach)]   // a team has one head coach
        [InlineData(UserRole.Player)]      // players are added through the roster
        public async Task CreateCoach_OtherRoles_AreRejected(UserRole role)
        {
            var headCoach = await _world.AddTeamAsync();

            using var context = _world.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _world.Users(context).CreateCoachAsync(headCoach, NewCoach(role)));
        }

        [Fact]
        public async Task CreateCoach_CoordinatorWithoutAUnit_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync();

            using var context = _world.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Users(context).CreateCoachAsync(headCoach, NewCoach(UserRole.Coordinator, unit: null)));
        }

        [Fact]
        public async Task CreateCoach_EmailAlreadyUsedOnAnotherTeam_IsRejected()
        {
            var tigersCoach = await _world.AddTeamAsync("Test Tigers");
            var bearsCoach = await _world.AddTeamAsync("Test Bears");

            using var context = _world.NewContext();
            await _world.Users(context).CreateCoachAsync(tigersCoach, NewCoach(email: "shared@example.com"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Users(context).CreateCoachAsync(bearsCoach, NewCoach(email: "SHARED@example.com")));
        }

        [Fact]
        public async Task Update_CanMoveACoachBetweenCoordinatorAndPositionCoach()
        {
            var headCoach = await _world.AddTeamAsync();
            var coach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");

            using (var context = _world.NewContext())
            {
                Assert.True(await _world.Users(context).UpdateAsync(headCoach, coach.Id, new UpdateUserDto
                {
                    FirstName = "Tom", LastName = "Gallagher", Email = "tom@example.com",
                    Role = UserRole.Coordinator, CoordinatorUnit = TeamUnit.Offence
                }));
            }

            var promoted = await _world.AsAsync(coach.Id);
            Assert.True(promoted.IsCoordinator);
            Assert.Equal(TeamUnit.Offence, promoted.CoordinatorUnit);
        }

        [Fact]
        public async Task Update_CannotTurnAPlayerIntoACoach_OrACoachIntoTheHeadCoach()
        {
            var headCoach = await _world.AddTeamAsync();
            var coach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach);
            var player = await _world.AddPlayerAsync(headCoach, "Walker");

            using var context = _world.NewContext();
            var service = _world.Users(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(headCoach, player.Id, new UpdateUserDto
            {
                FirstName = "P", LastName = "W", Email = "p@example.com", Role = UserRole.PositionCoach
            }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(headCoach, coach.Id, new UpdateUserDto
            {
                FirstName = "C", LastName = "C", Email = "c@example.com", Role = UserRole.HeadCoach
            }));
        }

        [Fact]
        public async Task Permissions_ApplyToAssistantCoachesOnly()
        {
            var headCoach = await _world.AddTeamAsync();
            var coach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach);
            var player = await _world.AddPlayerAsync(headCoach, "Walker");
            var switches = new UpdatePermissionsDto { CanPublishPlaybook = true, CanEditDepthChart = false, CanRecordAttendance = false };

            using var context = _world.NewContext();
            var service = _world.Users(context);

            Assert.True(await service.UpdatePermissionsAsync(headCoach, coach.Id, switches));
            var updated = await _world.AsAsync(coach.Id);
            Assert.True(updated.CanPublishPlaybook);
            Assert.False(updated.CanEditDepthChart);
            Assert.False(updated.CanRecordAttendance);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdatePermissionsAsync(headCoach, player.Id, switches));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdatePermissionsAsync(headCoach, headCoach.Id, switches));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdatePermissionsAsync(coach, coach.Id, switches));
        }

        [Fact]
        public async Task Deactivate_SignsTheUserOut_AndBlocksSignIn()
        {
            var headCoach = await _world.AddTeamAsync();
            TemporaryPasswordDto created;
            LoginResultDto signedIn;
            using (var context = _world.NewContext())
            {
                created = await _world.Users(context).CreateCoachAsync(headCoach, NewCoach());
                signedIn = (await _world.Auth(context).LoginAsync(new LoginDto { Email = created.Email, Password = created.TemporaryPassword }))!;
            }

            using (var context = _world.NewContext())
            {
                Assert.True(await _world.Users(context).SetActiveAsync(headCoach, created.UserId, false));
            }

            using var checkContext = _world.NewContext();
            var auth = _world.Auth(checkContext);
            Assert.Null(await auth.ResolveCurrentUserAsync(created.UserId, signedIn.SessionToken));
            await Assert.ThrowsAsync<ForbiddenException>(
                () => auth.LoginAsync(new LoginDto { Email = created.Email, Password = created.TemporaryPassword }));
        }

        [Fact]
        public async Task Deactivate_TheHeadCoach_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync();

            using var context = _world.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _world.Users(context).SetActiveAsync(headCoach, headCoach.Id, false));
        }

        [Fact]
        public async Task ResetPassword_GivesANewTemporaryPassword_AndEndsOpenSessions()
        {
            var headCoach = await _world.AddTeamAsync();
            TemporaryPasswordDto created;
            LoginResultDto signedIn;
            using (var context = _world.NewContext())
            {
                created = await _world.Users(context).CreateCoachAsync(headCoach, NewCoach());
                signedIn = (await _world.Auth(context).LoginAsync(new LoginDto { Email = created.Email, Password = created.TemporaryPassword }))!;
            }

            TemporaryPasswordDto reset;
            using (var context = _world.NewContext())
            {
                reset = (await _world.Users(context).ResetPasswordAsync(headCoach, created.UserId))!;
            }

            Assert.NotEqual(created.TemporaryPassword, reset.TemporaryPassword);

            using var checkContext = _world.NewContext();
            var auth = _world.Auth(checkContext);
            Assert.Null(await auth.ResolveCurrentUserAsync(created.UserId, signedIn.SessionToken));
            Assert.Null(await auth.LoginAsync(new LoginDto { Email = created.Email, Password = created.TemporaryPassword }));

            var withNewPassword = await auth.LoginAsync(new LoginDto { Email = created.Email, Password = reset.TemporaryPassword });
            Assert.True(withNewPassword!.MustChangePassword);
        }

        [Fact]
        public async Task ResetPassword_OfYourOwnAccount_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync();

            using var context = _world.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _world.Users(context).ResetPasswordAsync(headCoach, headCoach.Id));
        }

        [Fact]
        public async Task StaffList_ShowsOnlyThisTeamsCoaches_AndHidesEmailsFromPlayers()
        {
            var tigersCoach = await _world.AddTeamAsync("Test Tigers");
            await _world.AddCoachAsync(tigersCoach, UserRole.Coordinator, TeamUnit.Offence);
            var tigersPlayer = await _world.AddPlayerAsync(tigersCoach, "Walker");
            var bearsCoach = await _world.AddTeamAsync("Test Bears");
            await _world.AddCoachAsync(bearsCoach, UserRole.PositionCoach);

            using var context = _world.NewContext();
            var service = _world.Users(context);

            var seenByCoach = await service.GetStaffAsync(tigersCoach);
            Assert.Equal(2, seenByCoach.Count);   // head coach and coordinator; no players, no other team
            Assert.All(seenByCoach, c => Assert.False(string.IsNullOrEmpty(c.Email)));

            var seenByPlayer = await service.GetStaffAsync(tigersPlayer);
            Assert.Equal(2, seenByPlayer.Count);
            Assert.All(seenByPlayer, c => Assert.Null(c.Email));
        }

        [Fact]
        public async Task AnotherTeamsAccounts_CannotBeReached()
        {
            var tigersCoach = await _world.AddTeamAsync("Test Tigers");
            var tigersAssistant = await _world.AddCoachAsync(tigersCoach, UserRole.PositionCoach);
            var bearsCoach = await _world.AddTeamAsync("Test Bears");

            using var context = _world.NewContext();
            var service = _world.Users(context);

            Assert.Null(await service.GetByIdAsync(bearsCoach, tigersAssistant.Id));
            Assert.Null(await service.ResetPasswordAsync(bearsCoach, tigersAssistant.Id));
            Assert.False(await service.SetActiveAsync(bearsCoach, tigersAssistant.Id, false));
            Assert.False(await service.UpdatePermissionsAsync(bearsCoach, tigersAssistant.Id, new UpdatePermissionsDto()));
            Assert.False(await service.UpdateAsync(bearsCoach, tigersAssistant.Id, new UpdateUserDto
            {
                FirstName = "X", LastName = "Y", Email = "x@example.com", Role = UserRole.PositionCoach
            }));

            // Nothing about the other team's coach changed
            Assert.True((await context.Users.AsNoTracking().SingleAsync(u => u.Id == tigersAssistant.Id)).IsActive);
        }
    }
}
