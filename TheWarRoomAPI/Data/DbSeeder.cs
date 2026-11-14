using Microsoft.AspNetCore.Identity;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Data
{
    // Adds demo teams to an empty database so the app has something to show.
    // It runs at startup when "Seed:DemoData" is true (see appsettings.Development.json).
    // Every demo account uses the password in "Seed:DemoPassword".
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context, string demoPassword, TimeProvider clock)
        {
            // The demo data is added only once
            if (context.Teams.Any())
            {
                return;
            }

            var passwordHasher = new PasswordHasher<User>();
            var now = clock.GetUtcNow().UtcDateTime;

            // The main demo team
            var lions = new Team
            {
                Name = "Lakeshore Lions",
                AttendanceTrackingEnabled = true,
                CreatedAt = now
            };
            context.Users.Add(CreateUser(lions, "Marcus", "Bennett", "coach.bennett@lakeshorelions.example",
                UserRole.HeadCoach, null, demoPassword, passwordHasher, now));

            // A second, small team. It exists to show that one team can never see another team's data.
            var knights = new Team
            {
                Name = "Northgate Knights",
                AttendanceTrackingEnabled = false,
                CreatedAt = now
            };
            context.Users.Add(CreateUser(knights, "Dana", "Whitfield", "coach.whitfield@northgateknights.example",
                UserRole.HeadCoach, null, demoPassword, passwordHasher, now));

            context.SaveChanges();
        }

        private static User CreateUser(Team team, string firstName, string lastName, string email,
            UserRole role, TeamUnit? coordinatorUnit, string password, PasswordHasher<User> passwordHasher, DateTime now)
        {
            var user = new User
            {
                Team = team,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Role = role,
                CoordinatorUnit = coordinatorUnit,
                CanPublishPlaybook = role == UserRole.HeadCoach,
                CanEditDepthChart = true,
                CanRecordAttendance = true,
                MustChangePassword = false,
                IsActive = true,
                CreatedAt = now
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            return user;
        }
    }
}
