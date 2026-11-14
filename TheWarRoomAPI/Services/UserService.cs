using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly TimeProvider _clock;

        public UserService(AppDbContext context, IPasswordHasher<User> passwordHasher, TimeProvider clock)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _clock = clock;
        }

        public async Task<List<UserDto>> GetStaffAsync(CurrentUser currentUser)
        {
            var staff = await _context.Users
                .Where(u => u.TeamId == currentUser.TeamId && u.Role != UserRole.Player)
                .Include(u => u.CoachGroupAssignments!)
                    .ThenInclude(a => a.PositionGroup)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            // Players see who the coaches are, but not their email addresses
            return staff.Select(u => ToDto(u, includeEmail: currentUser.IsCoach)).ToList();
        }

        public async Task<UserDto?> GetByIdAsync(CurrentUser currentUser, int id)
        {
            var user = await _context.Users
                .Include(u => u.CoachGroupAssignments!)
                    .ThenInclude(a => a.PositionGroup)
                .FirstOrDefaultAsync(u => u.Id == id && u.TeamId == currentUser.TeamId);
            if (user == null)
            {
                return null;
            }

            bool includeEmail = currentUser.IsCoach || currentUser.Id == user.Id;
            return ToDto(user, includeEmail);
        }

        public async Task<TemporaryPasswordDto> CreateCoachAsync(CurrentUser currentUser, CreateCoachDto dto)
        {
            EnsureHeadCoach(currentUser);

            // Business rule: a team has one head coach, and players are added through the roster
            if (dto.Role != UserRole.Coordinator && dto.Role != UserRole.PositionCoach)
            {
                throw new InvalidOperationException("A new coach must be a coordinator or a position coach.");
            }

            var unit = CheckCoordinatorUnit(dto.Role, dto.CoordinatorUnit);

            var email = dto.Email.Trim();
            await AccountRules.EnsureEmailIsUniqueAsync(_context, email);

            var temporaryPassword = TemporaryPassword.Create();

            var coach = new User
            {
                TeamId = currentUser.TeamId,
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Email = email,
                Role = dto.Role,
                CoordinatorUnit = unit,
                CanPublishPlaybook = false,     // playbook changes need approval until the head coach says otherwise
                CanEditDepthChart = true,
                CanRecordAttendance = true,
                MustChangePassword = true,      // the temporary password must be replaced at first sign-in
                IsActive = true,
                CreatedAt = _clock.GetUtcNow().UtcDateTime
            };
            coach.PasswordHash = _passwordHasher.HashPassword(coach, temporaryPassword);

            _context.Users.Add(coach);
            await _context.SaveChangesAsync();

            return ToTemporaryPasswordDto(coach, temporaryPassword);
        }

        public async Task<bool> UpdateAsync(CurrentUser currentUser, int id, UpdateUserDto dto)
        {
            EnsureHeadCoach(currentUser);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.TeamId == currentUser.TeamId);
            if (user == null)
            {
                return false;
            }

            // Business rule: the head coach stays the head coach and a player stays a player.
            // A coach can only move between coordinator and position coach.
            if (dto.Role != user.Role)
            {
                bool wasAssistant = user.Role == UserRole.Coordinator || user.Role == UserRole.PositionCoach;
                bool willBeAssistant = dto.Role == UserRole.Coordinator || dto.Role == UserRole.PositionCoach;
                if (!wasAssistant || !willBeAssistant)
                {
                    throw new InvalidOperationException("A role can only be changed between coordinator and position coach.");
                }
            }

            var unit = CheckCoordinatorUnit(dto.Role, dto.CoordinatorUnit);

            var email = dto.Email.Trim();
            await AccountRules.EnsureEmailIsUniqueAsync(_context, email, user.Id);

            user.FirstName = dto.FirstName.Trim();
            user.LastName = dto.LastName.Trim();
            user.Email = email;
            user.Role = dto.Role;
            user.CoordinatorUnit = unit;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdatePermissionsAsync(CurrentUser currentUser, int id, UpdatePermissionsDto dto)
        {
            EnsureHeadCoach(currentUser);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.TeamId == currentUser.TeamId);
            if (user == null)
            {
                return false;
            }

            // Business rule: the switches apply to coordinators and position coaches.
            // The head coach always has every permission, and players have none.
            if (user.Role != UserRole.Coordinator && user.Role != UserRole.PositionCoach)
            {
                throw new InvalidOperationException("Permissions can only be set for coordinators and position coaches.");
            }

            user.CanPublishPlaybook = dto.CanPublishPlaybook;
            user.CanEditDepthChart = dto.CanEditDepthChart;
            user.CanRecordAttendance = dto.CanRecordAttendance;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetActiveAsync(CurrentUser currentUser, int id, bool isActive)
        {
            EnsureHeadCoach(currentUser);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.TeamId == currentUser.TeamId);
            if (user == null)
            {
                return false;
            }

            // Business rule: a team always keeps its head coach
            if (!isActive && user.Role == UserRole.HeadCoach)
            {
                throw new InvalidOperationException("The head coach account cannot be deactivated.");
            }

            user.IsActive = isActive;

            if (!isActive)
            {
                // A deactivated user is signed out everywhere
                await AccountRules.EndOpenSessionsAsync(_context, user.Id);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<TemporaryPasswordDto?> ResetPasswordAsync(CurrentUser currentUser, int id)
        {
            EnsureHeadCoach(currentUser);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.TeamId == currentUser.TeamId);
            if (user == null)
            {
                return null;
            }

            // Business rule: your own password is changed on the Change password page, where the current one is confirmed
            if (user.Id == currentUser.Id)
            {
                throw new InvalidOperationException("Use Change password to change your own password.");
            }

            var temporaryPassword = TemporaryPassword.Create();
            user.PasswordHash = _passwordHasher.HashPassword(user, temporaryPassword);
            user.MustChangePassword = true;

            // Whoever was signed in with the old password is signed out
            await AccountRules.EndOpenSessionsAsync(_context, user.Id);

            await _context.SaveChangesAsync();

            return ToTemporaryPasswordDto(user, temporaryPassword);
        }

        private static void EnsureHeadCoach(CurrentUser currentUser)
        {
            if (!AccessRules.CanManageTeam(currentUser))
            {
                throw new ForbiddenException("Only the head coach can manage accounts.");
            }
        }

        // Business rule: a coordinator runs exactly one unit; a position coach has none
        private static TeamUnit? CheckCoordinatorUnit(UserRole role, TeamUnit? unit)
        {
            if (role == UserRole.Coordinator)
            {
                if (unit == null)
                {
                    throw new InvalidOperationException("Choose the unit this coordinator runs.");
                }
                return unit;
            }

            return null;
        }

        // Map entity → DTO
        private static UserDto ToDto(User user, bool includeEmail)
        {
            return new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = includeEmail ? user.Email : null,
                Role = user.Role,
                CoordinatorUnit = user.CoordinatorUnit,
                CanPublishPlaybook = user.CanPublishPlaybook,
                CanEditDepthChart = user.CanEditDepthChart,
                CanRecordAttendance = user.CanRecordAttendance,
                MustChangePassword = user.MustChangePassword,
                IsActive = user.IsActive,
                Groups = (user.CoachGroupAssignments ?? new List<CoachGroupAssignment>())
                    .Where(a => a.PositionGroup != null)
                    .OrderBy(a => a.PositionGroup!.SortOrder)
                    .Select(a => new GroupSummaryDto
                    {
                        Id = a.PositionGroupId,
                        Name = a.PositionGroup!.Name,
                        Unit = a.PositionGroup.Unit
                    })
                    .ToList()
            };
        }

        private static TemporaryPasswordDto ToTemporaryPasswordDto(User user, string temporaryPassword)
        {
            return new TemporaryPasswordDto
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                TemporaryPassword = temporaryPassword
            };
        }
    }
}
