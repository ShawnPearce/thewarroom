using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Sets up teams, coaches and players for a test, and creates the services under test.
    // Accounts are written straight to the database here, which is quicker than going through
    // the services; the tests about creating accounts use the services themselves.
    public sealed class TestWorld : IDisposable
    {
        private int _accountCounter;

        public TestDatabase Database { get; } = new TestDatabase();

        // Wednesday 2 September 2026. The Monday of that week is 31 August.
        public TestClock Clock { get; } = new TestClock(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));

        public static readonly DateOnly ThisMonday = new DateOnly(2026, 8, 31);

        public PasswordHasher<User> PasswordHasher { get; } = new PasswordHasher<User>();

        public void Dispose()
        {
            Database.Dispose();
        }

        public AppDbContext NewContext()
        {
            return Database.CreateContext();
        }

        // ---------- Services ----------

        public AuthService Auth(AppDbContext context) => new AuthService(context, PasswordHasher, Clock);
        public UserService Users(AppDbContext context) => new UserService(context, PasswordHasher, Clock);
        public RosterService Roster(AppDbContext context) => new RosterService(context, PasswordHasher, Clock);
        public PositionGroupService Groups(AppDbContext context) => new PositionGroupService(context, Clock);
        public DepthChartService DepthChart(AppDbContext context) => new DepthChartService(context, Clock);

        // ---------- Setting up data ----------

        // A team with the standard groups and positions, and its head coach
        public async Task<CurrentUser> AddTeamAsync(string name = "Test Tigers")
        {
            using var context = NewContext();

            var team = new Team { Name = name, CreatedAt = Clock.GetUtcNow().UtcDateTime };
            var headCoach = NewUser(team, UserRole.HeadCoach, "Head", "Coach");
            headCoach.CanPublishPlaybook = true;

            context.Users.Add(headCoach);
            TeamDefaults.AddTo(team, context);
            await context.SaveChangesAsync();

            return await AsAsync(headCoach.Id);
        }

        public async Task<CurrentUser> AddCoachAsync(CurrentUser teamMember, UserRole role, TeamUnit? unit = null, params string[] groupNames)
        {
            using var context = NewContext();

            var coach = NewUser(null, role, "Coach", $"Number{_accountCounter + 1}");
            coach.TeamId = teamMember.TeamId;
            coach.CoordinatorUnit = unit;
            coach.CoachGroupAssignments = new List<CoachGroupAssignment>();
            foreach (var groupName in groupNames)
            {
                coach.CoachGroupAssignments.Add(new CoachGroupAssignment { PositionGroupId = await GroupIdAsync(teamMember, groupName) });
            }

            context.Users.Add(coach);
            await context.SaveChangesAsync();

            return await AsAsync(coach.Id);
        }

        public async Task<CurrentUser> AddPlayerAsync(CurrentUser teamMember, string lastName, params string[] groupNames)
        {
            using var context = NewContext();

            var player = NewUser(null, UserRole.Player, "Player", lastName);
            player.TeamId = teamMember.TeamId;
            player.CanEditDepthChart = false;
            player.CanRecordAttendance = false;
            player.RosterEntry = new RosterEntry { TeamId = teamMember.TeamId, Status = RosterStatus.Active };
            player.PlayerGroupAssignments = new List<PlayerGroupAssignment>();
            foreach (var groupName in groupNames)
            {
                player.PlayerGroupAssignments.Add(new PlayerGroupAssignment { PositionGroupId = await GroupIdAsync(teamMember, groupName) });
            }

            context.Users.Add(player);
            await context.SaveChangesAsync();

            return await AsAsync(player.Id);
        }

        public async Task<int> AddWeekAsync(CurrentUser teamMember, DateOnly monday)
        {
            using var context = NewContext();

            var week = new DepthChartWeek
            {
                TeamId = teamMember.TeamId,
                WeekStartDate = monday,
                CreatedAt = Clock.GetUtcNow().UtcDateTime
            };
            context.DepthChartWeeks.Add(week);
            await context.SaveChangesAsync();

            return week.Id;
        }

        // ---------- Looking things up ----------

        // The user as the API would see them on their next request (role, permissions and groups re-read)
        public async Task<CurrentUser> AsAsync(int userId)
        {
            using var context = NewContext();
            var currentUser = await Auth(context).ResolveCurrentUserAsync(userId, null);
            return currentUser ?? throw new InvalidOperationException($"User {userId} could not be resolved.");
        }

        public async Task<int> GroupIdAsync(CurrentUser teamMember, string groupName)
        {
            using var context = NewContext();
            return await context.PositionGroups
                .Where(g => g.TeamId == teamMember.TeamId && g.Name == groupName)
                .Select(g => g.Id)
                .SingleAsync();
        }

        public async Task<int> PositionIdAsync(CurrentUser teamMember, string code)
        {
            using var context = NewContext();
            return await context.Positions
                .Where(p => p.TeamId == teamMember.TeamId && p.Code == code)
                .Select(p => p.Id)
                .SingleAsync();
        }

        private User NewUser(Team? team, UserRole role, string firstName, string lastName)
        {
            _accountCounter++;

            return new User
            {
                Team = team,
                FirstName = firstName,
                LastName = lastName,
                Email = $"user{_accountCounter}@example.com",
                PasswordHash = "not-a-real-hash",
                Role = role,
                CanEditDepthChart = true,
                CanRecordAttendance = true,
                IsActive = true,
                CreatedAt = Clock.GetUtcNow().UtcDateTime
            };
        }
    }
}
