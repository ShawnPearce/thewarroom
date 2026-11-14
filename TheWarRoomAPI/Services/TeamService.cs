using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class TeamService : ITeamService
    {
        private readonly AppDbContext _context;
        private readonly IAuthService _authService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly TimeProvider _clock;

        public TeamService(AppDbContext context, IAuthService authService, IPasswordHasher<User> passwordHasher, TimeProvider clock)
        {
            _context = context;
            _authService = authService;
            _passwordHasher = passwordHasher;
            _clock = clock;
        }

        public async Task<LoginResultDto> RegisterAsync(RegisterTeamDto dto)
        {
            var email = dto.Email.Trim();

            // Business rule: an email address can belong to only one account
            await EnsureEmailIsUniqueAsync(email);

            var now = _clock.GetUtcNow().UtcDateTime;

            var team = new Team
            {
                Name = dto.TeamName.Trim(),
                AttendanceTrackingEnabled = false,
                CreatedAt = now
            };

            // The person who registers the team becomes its head coach
            var headCoach = new User
            {
                Team = team,
                Email = email,
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Role = UserRole.HeadCoach,
                CanPublishPlaybook = true,
                CanEditDepthChart = true,
                CanRecordAttendance = true,
                MustChangePassword = false,
                IsActive = true,
                CreatedAt = now
            };
            headCoach.PasswordHash = _passwordHasher.HashPassword(headCoach, dto.Password);

            _context.Users.Add(headCoach);   // the team is saved with it through the navigation property
            await _context.SaveChangesAsync();

            return await _authService.StartSessionAsync(headCoach);
        }

        public async Task<TeamDto?> GetAsync(CurrentUser currentUser)
        {
            return await _context.Teams
                .Where(t => t.Id == currentUser.TeamId)
                .Select(t => new TeamDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    AttendanceTrackingEnabled = t.AttendanceTrackingEnabled,
                    CreatedAt = t.CreatedAt
                })
                .FirstOrDefaultAsync();
        }

        private async Task EnsureEmailIsUniqueAsync(string email)
        {
            bool exists = await _context.Users.AnyAsync(u => u.Email == email);
            if (exists)
            {
                throw new InvalidOperationException("An account with this email address already exists.");
            }
        }
    }
}
