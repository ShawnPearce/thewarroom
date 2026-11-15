using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        public void ADatabaseFromBeforeTeamsChoseTheirGame_IsBroughtUpToDateAsAmerican()
        {
            using var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

            using var context = new AppDbContext(options);
            var migrator = context.GetService<IMigrator>();

            // The database as it was before a team could choose its game, with a team already in it
            migrator.Migrate("AddCutupRendering");
            context.Database.ExecuteSqlRaw("""
                INSERT INTO Teams (Id, Name, AttendanceTrackingEnabled, CreatedAt) VALUES (1, 'Test Tigers', 0, '2026-09-01 12:00:00');
                INSERT INTO PositionGroups (Id, TeamId, Unit, Name, SortOrder) VALUES (1, 1, 1, 'Tight Ends', 1), (2, 1, 2, 'Defensive Backs', 2);
                INSERT INTO Positions (Id, TeamId, PositionGroupId, Code, Name, SortOrder) VALUES
                    (1, 1, 1, 'TE', 'Tight End', 1), (2, 1, 2, 'FS', 'Free Safety', 2), (3, 1, 2, 'SS', 'Strong Safety', 3),
                    (4, 1, 2, 'LCB', 'Left Cornerback', 4), (5, 1, 2, 'S', 'Single-high Safety', 5);
                INSERT INTO Playbooks (Id, TeamId, Unit, Name) VALUES (1, 1, 1, 'Offence playbook');
                """);

            migrator.Migrate();

            // The team and its playbook are American football, which is what they always were
            Assert.Equal(FootballRules.American, context.Teams.Single().Rules);
            Assert.Equal(FootballRules.American, context.Playbooks.Single().Rules);

            // The three standard positions only the American game has are marked, so they can be put away later.
            // A position both games have, and one the team made itself, are left for both games.
            var positions = context.Positions.ToDictionary(p => p.Code, p => p.Rules);
            Assert.Equal(FootballRules.American, positions["TE"]);
            Assert.Equal(FootballRules.American, positions["FS"]);
            Assert.Equal(FootballRules.American, positions["SS"]);
            Assert.Null(positions["LCB"]);
            Assert.Null(positions["S"]);
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
