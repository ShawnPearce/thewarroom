using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly TimeProvider _clock;

        public AuthService(AppDbContext context, IPasswordHasher<User> passwordHasher, TimeProvider clock)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _clock = clock;
        }

        public async Task<LoginResultDto?> LoginAsync(LoginDto dto)
        {
            var email = dto.Email.Trim();

            var user = await _context.Users
                .Include(u => u.Team)
                .FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return null;
            }

            var check = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (check == PasswordVerificationResult.Failed)
            {
                return null;
            }

            // Business rule: a deactivated account cannot sign in
            if (!user.IsActive)
            {
                throw new ForbiddenException("This account has been deactivated. Ask your head coach to reactivate it.");
            }

            return await StartSessionAsync(user);
        }

        public async Task<LoginResultDto> StartSessionAsync(User user)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            var session = new LoginSession
            {
                UserId = user.Id,
                Token = Guid.NewGuid().ToString("N"),
                StartedAt = now,
                LastSeenAt = now
            };
            _context.LoginSessions.Add(session);
            await _context.SaveChangesAsync();

            var teamName = user.Team?.Name
                ?? await _context.Teams.Where(t => t.Id == user.TeamId).Select(t => t.Name).FirstAsync();

            // Map entity → DTO
            return new LoginResultDto
            {
                UserId = user.Id,
                TeamId = user.TeamId,
                TeamName = teamName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                CoordinatorUnit = user.CoordinatorUnit,
                MustChangePassword = user.MustChangePassword,
                SessionToken = session.Token
            };
        }

        public async Task LogoutAsync(CurrentUser currentUser)
        {
            if (currentUser.LoginSessionId == null)
            {
                return;
            }

            var session = await _context.LoginSessions.FindAsync(currentUser.LoginSessionId.Value);
            if (session != null && session.EndedAt == null)
            {
                var now = _clock.GetUtcNow().UtcDateTime;
                session.LastSeenAt = now;
                session.EndedAt = now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task ChangePasswordAsync(CurrentUser currentUser, ChangePasswordDto dto)
        {
            var user = await _context.Users.FirstAsync(u => u.Id == currentUser.Id);

            // Business rule: the current password must be confirmed before it can be replaced
            var check = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword);
            if (check == PasswordVerificationResult.Failed)
            {
                throw new InvalidOperationException("The current password is not correct.");
            }

            // Business rule: the new password must be different from the old one
            if (dto.NewPassword == dto.CurrentPassword)
            {
                throw new InvalidOperationException("The new password must be different from the current password.");
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
            user.MustChangePassword = false;
            await _context.SaveChangesAsync();
        }

        public async Task<CurrentUserDto?> GetMeAsync(CurrentUser currentUser)
        {
            return await _context.Users
                .Where(u => u.Id == currentUser.Id)
                .Select(u => new CurrentUserDto
                {
                    Id = u.Id,
                    TeamId = u.TeamId,
                    TeamName = u.Team!.Name,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Role = u.Role,
                    CoordinatorUnit = u.CoordinatorUnit,
                    CanPublishPlaybook = u.CanPublishPlaybook,
                    CanEditDepthChart = u.CanEditDepthChart,
                    CanRecordAttendance = u.CanRecordAttendance,
                    MustChangePassword = u.MustChangePassword
                })
                .FirstOrDefaultAsync();
        }

        public async Task<CurrentUser?> ResolveCurrentUserAsync(int userId, string? sessionToken)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (user == null)
            {
                return null;
            }

            int? loginSessionId = null;
            if (!string.IsNullOrWhiteSpace(sessionToken))
            {
                var session = await _context.LoginSessions.FirstOrDefaultAsync(s => s.Token == sessionToken);

                // The token must belong to this user, and the session must not have been signed out
                if (session == null || session.UserId != userId || session.EndedAt != null)
                {
                    return null;
                }

                // Record that the user is still active, at most once a minute
                var now = _clock.GetUtcNow().UtcDateTime;
                if (now - session.LastSeenAt >= TimeSpan.FromMinutes(1))
                {
                    session.LastSeenAt = now;
                    await _context.SaveChangesAsync();
                }

                loginSessionId = session.Id;
            }

            return new CurrentUser
            {
                Id = user.Id,
                TeamId = user.TeamId,
                Role = user.Role,
                CoordinatorUnit = user.CoordinatorUnit,
                CanPublishPlaybook = user.CanPublishPlaybook,
                CanEditDepthChart = user.CanEditDepthChart,
                CanRecordAttendance = user.CanRecordAttendance,
                LoginSessionId = loginSessionId
            };
        }
    }
}
