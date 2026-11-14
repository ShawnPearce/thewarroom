using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The database itself: migrations, how values are stored, and the demo data
    public class DatabaseTests
    {
        [Fact]
        public void Migrations_ApplyToAnEmptyDatabase_AndMatchTheModel()
        {
            using var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

            using var context = new AppDbContext(options);
            context.Database.Migrate();

            // False means every change to the entity classes has a migration
            Assert.False(context.Database.HasPendingModelChanges());
        }

        [Fact]
        public async Task DateTimes_AreReadBackAsUtc()
        {
            using var database = new TestDatabase();
            var moment = new DateTime(2026, 9, 1, 18, 30, 0, DateTimeKind.Utc);

            using (var context = database.CreateContext())
            {
                var team = new Team { Name = "Test Tigers", CreatedAt = moment };
                var user = new User
                {
                    Team = team, Email = "coach@example.com", PasswordHash = "x",
                    FirstName = "Casey", LastName = "Coach", Role = UserRole.HeadCoach, CreatedAt = moment
                };
                context.LoginSessions.Add(new LoginSession
                {
                    User = user, Token = "token", StartedAt = moment, LastSeenAt = moment, EndedAt = moment
                });
                await context.SaveChangesAsync();
            }

            using var readContext = database.CreateContext();
            var session = await readContext.LoginSessions.SingleAsync();

            Assert.Equal(DateTimeKind.Utc, session.StartedAt.Kind);
            Assert.Equal(DateTimeKind.Utc, session.EndedAt!.Value.Kind);   // nullable DateTime as well
            Assert.Equal(moment, session.StartedAt);
        }

        [Fact]
        public async Task DeletingATeamThatHasUsers_IsRefused()
        {
            using var database = new TestDatabase();
            using (var context = database.CreateContext())
            {
                var team = new Team { Name = "Test Tigers", CreatedAt = DateTime.UtcNow };
                context.Users.Add(new User
                {
                    Team = team, Email = "coach@example.com", PasswordHash = "x",
                    FirstName = "Casey", LastName = "Coach", Role = UserRole.HeadCoach, CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            // Nothing cascades by default, so the users are not silently deleted with their team
            using var deleteContext = database.CreateContext();
            deleteContext.Teams.Remove(await deleteContext.Teams.SingleAsync());
            await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());
        }

        [Fact]
        public async Task DemoData_IsAddedOnce_AndItsAccountsCanSignIn()
        {
            using var database = new TestDatabase();
            const string demoPassword = "Demo-Password-1";

            using (var context = database.CreateContext())
            {
                DbSeeder.Seed(context, demoPassword, TimeProvider.System);
                DbSeeder.Seed(context, demoPassword, TimeProvider.System);   // a second run must change nothing
            }

            using var checkContext = database.CreateContext();
            Assert.Equal(2, await checkContext.Teams.CountAsync());

            var headCoach = await checkContext.Users.FirstAsync(u => u.Role == UserRole.HeadCoach);
            var auth = new AuthService(checkContext, new PasswordHasher<User>(), TimeProvider.System);
            var result = await auth.LoginAsync(new LoginDto { Email = headCoach.Email, Password = demoPassword });

            Assert.NotNull(result);
        }
    }
}
