using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Team registration, signing in, passwords and login sessions
    public class AuthServiceTests : IDisposable
    {
        private const string Email = "coach@example.com";
        private const string Password = "Correct-Horse-1";

        private readonly TestDatabase _database = new TestDatabase();
        private readonly TestClock _clock = new TestClock(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();

        public void Dispose()
        {
            _database.Dispose();
        }

        private AuthService CreateAuthService(AppDbContext context)
        {
            return new AuthService(context, _passwordHasher, _clock);
        }

        private TeamService CreateTeamService(AppDbContext context)
        {
            return new TeamService(context, CreateAuthService(context), _passwordHasher, _clock);
        }

        private async Task<LoginResultDto> RegisterTeamAsync(string email = Email, string teamName = "Test Tigers")
        {
            using var context = _database.CreateContext();
            return await CreateTeamService(context).RegisterAsync(new RegisterTeamDto
            {
                TeamName = teamName,
                FirstName = "Casey",
                LastName = "Coach",
                Email = email,
                Password = Password
            });
        }

        private async Task<CurrentUser> ResolveAsync(int userId, string? token = null)
        {
            using var context = _database.CreateContext();
            var currentUser = await CreateAuthService(context).ResolveCurrentUserAsync(userId, token);
            Assert.NotNull(currentUser);
            return currentUser!;
        }

        // ---------- Registering a team ----------

        [Fact]
        public async Task Register_CreatesTheTeamAndItsHeadCoach()
        {
            var result = await RegisterTeamAsync();

            using var context = _database.CreateContext();
            var user = await context.Users.Include(u => u.Team).SingleAsync();

            Assert.Equal("Test Tigers", user.Team!.Name);
            Assert.Equal(UserRole.HeadCoach, user.Role);
            Assert.True(user.IsActive);
            Assert.False(user.MustChangePassword);
            Assert.Equal(user.Id, result.UserId);
            Assert.Equal(user.TeamId, result.TeamId);
            Assert.False(string.IsNullOrEmpty(result.SessionToken));
        }

        [Fact]
        public async Task Register_StoresAHashAndNotThePassword()
        {
            await RegisterTeamAsync();

            using var context = _database.CreateContext();
            var user = await context.Users.SingleAsync();

            Assert.NotEqual(Password, user.PasswordHash);
            Assert.DoesNotContain(Password, user.PasswordHash);
        }

        [Fact]
        public async Task Register_EmailAlreadyUsedInAnotherCase_IsRejected()
        {
            await RegisterTeamAsync("coach@example.com");

            // Business rule: an email address can belong to only one account, whatever its capital letters
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => RegisterTeamAsync("COACH@Example.com", "Second Team"));

            Assert.Contains("already exists", error.Message);
        }

        // ---------- Signing in ----------

        [Fact]
        public async Task Login_CorrectPassword_OpensANewSession()
        {
            var registered = await RegisterTeamAsync();

            using var context = _database.CreateContext();
            var result = await CreateAuthService(context).LoginAsync(new LoginDto { Email = Email, Password = Password });

            Assert.NotNull(result);
            Assert.Equal(registered.UserId, result!.UserId);
            Assert.Equal("Test Tigers", result.TeamName);
            Assert.NotEqual(registered.SessionToken, result.SessionToken);
        }

        [Fact]
        public async Task Login_EmailTypedInAnotherCase_StillSignsIn()
        {
            await RegisterTeamAsync();

            using var context = _database.CreateContext();
            var result = await CreateAuthService(context).LoginAsync(new LoginDto { Email = "  COACH@EXAMPLE.COM ", Password = Password });

            Assert.NotNull(result);
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsNull()
        {
            await RegisterTeamAsync();

            using var context = _database.CreateContext();
            var result = await CreateAuthService(context).LoginAsync(new LoginDto { Email = Email, Password = "not-the-password" });

            Assert.Null(result);
        }

        [Fact]
        public async Task Login_UnknownEmail_ReturnsNull()
        {
            using var context = _database.CreateContext();
            var result = await CreateAuthService(context).LoginAsync(new LoginDto { Email = "nobody@example.com", Password = Password });

            Assert.Null(result);
        }

        [Fact]
        public async Task Login_DeactivatedAccount_IsForbidden()
        {
            var registered = await RegisterTeamAsync();
            using (var context = _database.CreateContext())
            {
                var user = await context.Users.SingleAsync(u => u.Id == registered.UserId);
                user.IsActive = false;
                await context.SaveChangesAsync();
            }

            using var loginContext = _database.CreateContext();
            await Assert.ThrowsAsync<ForbiddenException>(
                () => CreateAuthService(loginContext).LoginAsync(new LoginDto { Email = Email, Password = Password }));
        }

        // ---------- Changing the password ----------

        [Fact]
        public async Task ChangePassword_WrongCurrentPassword_IsRejected()
        {
            var registered = await RegisterTeamAsync();
            var currentUser = await ResolveAsync(registered.UserId);

            using var context = _database.CreateContext();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateAuthService(context).ChangePasswordAsync(currentUser, new ChangePasswordDto
                {
                    CurrentPassword = "not-the-password",
                    NewPassword = "Another-Pass-2"
                }));
        }

        [Fact]
        public async Task ChangePassword_SameAsTheCurrentOne_IsRejected()
        {
            var registered = await RegisterTeamAsync();
            var currentUser = await ResolveAsync(registered.UserId);

            using var context = _database.CreateContext();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateAuthService(context).ChangePasswordAsync(currentUser, new ChangePasswordDto
                {
                    CurrentPassword = Password,
                    NewPassword = Password
                }));
        }

        [Fact]
        public async Task ChangePassword_ReplacesThePasswordAndClearsTheTemporaryFlag()
        {
            var registered = await RegisterTeamAsync();
            using (var context = _database.CreateContext())
            {
                var user = await context.Users.SingleAsync(u => u.Id == registered.UserId);
                user.MustChangePassword = true;
                await context.SaveChangesAsync();
            }
            var currentUser = await ResolveAsync(registered.UserId);

            using (var context = _database.CreateContext())
            {
                await CreateAuthService(context).ChangePasswordAsync(currentUser, new ChangePasswordDto
                {
                    CurrentPassword = Password,
                    NewPassword = "Another-Pass-2"
                });
            }

            using var checkContext = _database.CreateContext();
            var auth = CreateAuthService(checkContext);
            Assert.Null(await auth.LoginAsync(new LoginDto { Email = Email, Password = Password }));

            var withNewPassword = await auth.LoginAsync(new LoginDto { Email = Email, Password = "Another-Pass-2" });
            Assert.NotNull(withNewPassword);
            Assert.False(withNewPassword!.MustChangePassword);
        }

        // ---------- Working out who a request is acting as ----------

        [Fact]
        public async Task ResolveCurrentUser_UnknownUser_ReturnsNull()
        {
            using var context = _database.CreateContext();
            var currentUser = await CreateAuthService(context).ResolveCurrentUserAsync(999, null);

            Assert.Null(currentUser);
        }

        [Fact]
        public async Task ResolveCurrentUser_DeactivatedUser_ReturnsNull()
        {
            var registered = await RegisterTeamAsync();
            using (var context = _database.CreateContext())
            {
                var user = await context.Users.SingleAsync(u => u.Id == registered.UserId);
                user.IsActive = false;
                await context.SaveChangesAsync();
            }

            using var resolveContext = _database.CreateContext();
            var currentUser = await CreateAuthService(resolveContext).ResolveCurrentUserAsync(registered.UserId, registered.SessionToken);

            Assert.Null(currentUser);
        }

        [Fact]
        public async Task ResolveCurrentUser_WithoutAToken_ReturnsTheUser()
        {
            var registered = await RegisterTeamAsync();

            var currentUser = await ResolveAsync(registered.UserId);

            Assert.Equal(registered.UserId, currentUser.Id);
            Assert.Equal(registered.TeamId, currentUser.TeamId);
            Assert.True(currentUser.IsHeadCoach);
            Assert.Null(currentUser.LoginSessionId);
        }

        [Fact]
        public async Task ResolveCurrentUser_WithItsOwnToken_ReturnsTheSession()
        {
            var registered = await RegisterTeamAsync();

            var currentUser = await ResolveAsync(registered.UserId, registered.SessionToken);

            Assert.NotNull(currentUser.LoginSessionId);
        }

        [Fact]
        public async Task ResolveCurrentUser_TokenOfAnotherUser_ReturnsNull()
        {
            var first = await RegisterTeamAsync("first@example.com", "First Team");
            var second = await RegisterTeamAsync("second@example.com", "Second Team");

            using var context = _database.CreateContext();
            var currentUser = await CreateAuthService(context).ResolveCurrentUserAsync(first.UserId, second.SessionToken);

            Assert.Null(currentUser);
        }

        [Fact]
        public async Task ResolveCurrentUser_TokenThatDoesNotExist_ReturnsNull()
        {
            var registered = await RegisterTeamAsync();

            using var context = _database.CreateContext();
            var currentUser = await CreateAuthService(context).ResolveCurrentUserAsync(registered.UserId, "made-up-token");

            Assert.Null(currentUser);
        }

        // ---------- Login sessions ----------

        [Fact]
        public async Task Logout_EndsTheSession_AndItsTokenStopsWorking()
        {
            var registered = await RegisterTeamAsync();
            var currentUser = await ResolveAsync(registered.UserId, registered.SessionToken);

            _clock.Advance(TimeSpan.FromMinutes(20));
            using (var context = _database.CreateContext())
            {
                await CreateAuthService(context).LogoutAsync(currentUser);
            }

            using var checkContext = _database.CreateContext();
            var session = await checkContext.LoginSessions.SingleAsync();
            Assert.Equal(TimeSpan.FromMinutes(20), session.EndedAt!.Value - session.StartedAt);

            var afterLogout = await CreateAuthService(checkContext).ResolveCurrentUserAsync(registered.UserId, registered.SessionToken);
            Assert.Null(afterLogout);
        }

        [Fact]
        public async Task LastSeen_IsUpdatedAtMostOnceAMinute()
        {
            var registered = await RegisterTeamAsync();
            var startedAt = _clock.GetUtcNow().UtcDateTime;

            // 30 seconds later: too soon to write again
            _clock.Advance(TimeSpan.FromSeconds(30));
            await ResolveAsync(registered.UserId, registered.SessionToken);
            using (var context = _database.CreateContext())
            {
                Assert.Equal(startedAt, (await context.LoginSessions.SingleAsync()).LastSeenAt);
            }

            // 90 seconds after the start: the session is marked as seen
            _clock.Advance(TimeSpan.FromSeconds(60));
            await ResolveAsync(registered.UserId, registered.SessionToken);
            using (var context = _database.CreateContext())
            {
                Assert.Equal(startedAt.AddSeconds(90), (await context.LoginSessions.SingleAsync()).LastSeenAt);
            }
        }
    }
}
