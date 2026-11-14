using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Players: adding them, editing roster details, and finding a player's coaches
    public class RosterServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        [Fact]
        public async Task CreatePlayer_CreatesTheAccountRosterEntryAndGroups()
        {
            var headCoach = await _world.AddTeamAsync();
            int receivers = await _world.GroupIdAsync(headCoach, "Receivers");
            int defensiveBacks = await _world.GroupIdAsync(headCoach, "Defensive Backs");

            TemporaryPasswordDto created;
            using (var context = _world.NewContext())
            {
                created = await _world.Roster(context).CreatePlayerAsync(headCoach, new CreatePlayerDto
                {
                    FirstName = "Noah", LastName = "Kim", Email = "noah.kim@example.com",
                    JerseyNumber = 11, Grade = 11,
                    GroupIds = new List<int> { receivers, defensiveBacks }
                });
            }

            using var checkContext = _world.NewContext();
            var player = await _world.Roster(checkContext).GetPlayerAsync(headCoach, created.UserId);

            Assert.NotNull(player);
            Assert.Equal(11, player!.JerseyNumber);
            Assert.Equal(RosterStatus.Active, player.Status);
            // One player, two groups, on offence and on defence
            Assert.Equal(new[] { TeamUnit.Offence, TeamUnit.Defence }, player.Groups.Select(g => g.Unit));

            var login = await _world.Auth(checkContext).LoginAsync(new LoginDto { Email = created.Email, Password = created.TemporaryPassword });
            Assert.Equal(UserRole.Player, login!.Role);
            Assert.True(login.MustChangePassword);
        }

        [Fact]
        public async Task CreatePlayer_OnlyTheHeadCoachMay()
        {
            var headCoach = await _world.AddTeamAsync();
            var coordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);

            using var context = _world.NewContext();
            await Assert.ThrowsAsync<ForbiddenException>(() => _world.Roster(context).CreatePlayerAsync(coordinator, new CreatePlayerDto
            {
                FirstName = "A", LastName = "B", Email = "a.b@example.com"
            }));
        }

        [Fact]
        public async Task CreatePlayer_InAGroupOfAnotherTeam_IsRejected()
        {
            var tigersCoach = await _world.AddTeamAsync("Test Tigers");
            var bearsCoach = await _world.AddTeamAsync("Test Bears");
            int bearsGroup = await _world.GroupIdAsync(bearsCoach, "Receivers");

            using var context = _world.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _world.Roster(context).CreatePlayerAsync(tigersCoach, new CreatePlayerDto
            {
                FirstName = "A", LastName = "B", Email = "a.b@example.com",
                GroupIds = new List<int> { bearsGroup }
            }));
        }

        [Fact]
        public async Task Update_HeadCoachAndTheUnitsCoordinatorMay_OthersMayNot()
        {
            var headCoach = await _world.AddTeamAsync();
            var offenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            var defenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Defence);
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var receiver = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var details = new UpdateRosterEntryDto { JerseyNumber = 1, Grade = 12, Status = RosterStatus.Injured };

            using var context = _world.NewContext();
            var service = _world.Roster(context);

            Assert.True(await service.UpdateAsync(headCoach, receiver.Id, details));
            Assert.True(await service.UpdateAsync(offenceCoordinator, receiver.Id, details));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(defenceCoordinator, receiver.Id, details));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(receiversCoach, receiver.Id, details));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(receiver, receiver.Id, details));

            var saved = await service.GetPlayerAsync(headCoach, receiver.Id);
            Assert.Equal(RosterStatus.Injured, saved!.Status);
            Assert.Equal(1, saved.JerseyNumber);
        }

        [Fact]
        public async Task Coaches_OfAPlayer_AreTheHeadCoachTheUnitCoordinatorsAndTheGroupCoaches()
        {
            var headCoach = await _world.AddTeamAsync();
            var offenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            var defenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Defence);
            var specialTeamsCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.SpecialTeams);
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers", "Tight Ends");
            var lineCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Offensive Line");

            // A two-way player: receiver on offence, defensive back on defence
            var player = await _world.AddPlayerAsync(headCoach, "Kim", "Receivers", "Defensive Backs");

            using var context = _world.NewContext();
            var coaches = await _world.Roster(context).GetCoachesAsync(player, player.Id);

            Assert.NotNull(coaches);
            var titles = coaches!.ToDictionary(c => c.Id, c => c.Title);
            Assert.Equal("Head coach", titles[headCoach.Id]);
            Assert.Equal("Offence coordinator", titles[offenceCoordinator.Id]);
            Assert.Equal("Defence coordinator", titles[defenceCoordinator.Id]);
            Assert.Equal("Receivers coach", titles[receiversCoach.Id]);
            Assert.False(titles.ContainsKey(specialTeamsCoordinator.Id));   // the player is in no special teams group
            Assert.False(titles.ContainsKey(lineCoach.Id));
        }

        [Fact]
        public async Task Coaches_APlayerCanOnlyLookUpTheirOwn()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var teammate = await _world.AddPlayerAsync(headCoach, "Hughes", "Receivers");

            using var context = _world.NewContext();
            await Assert.ThrowsAsync<ForbiddenException>(() => _world.Roster(context).GetCoachesAsync(player, teammate.Id));
        }

        [Fact]
        public async Task Roster_ShowsOnlyThisTeam_AndHidesTeammatesEmailsFromPlayers()
        {
            var tigersCoach = await _world.AddTeamAsync("Test Tigers");
            var walker = await _world.AddPlayerAsync(tigersCoach, "Walker", "Receivers");
            await _world.AddPlayerAsync(tigersCoach, "Hughes", "Receivers");
            var bearsCoach = await _world.AddTeamAsync("Test Bears");
            var bearsPlayer = await _world.AddPlayerAsync(bearsCoach, "Stone", "Receivers");

            using var context = _world.NewContext();
            var service = _world.Roster(context);

            var seenByCoach = await service.GetRosterAsync(tigersCoach);
            Assert.Equal(new[] { "Hughes", "Walker" }, seenByCoach.Select(p => p.LastName));
            Assert.All(seenByCoach, p => Assert.NotNull(p.Email));

            var seenByWalker = await service.GetRosterAsync(walker);
            Assert.NotNull(seenByWalker.Single(p => p.LastName == "Walker").Email);   // their own
            Assert.Null(seenByWalker.Single(p => p.LastName == "Hughes").Email);

            // Another team's player is "not found", for reading and for editing
            Assert.Null(await service.GetPlayerAsync(tigersCoach, bearsPlayer.Id));
            Assert.Null(await service.GetCoachesAsync(tigersCoach, bearsPlayer.Id));
            Assert.False(await service.UpdateAsync(tigersCoach, bearsPlayer.Id, new UpdateRosterEntryDto { Status = RosterStatus.Inactive }));
        }
    }
}
