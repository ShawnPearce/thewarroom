using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class RosterService : IRosterService
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly TimeProvider _clock;

        public RosterService(AppDbContext context, IPasswordHasher<User> passwordHasher, TimeProvider clock)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _clock = clock;
        }

        public async Task<List<RosterEntryDto>> GetRosterAsync(CurrentUser currentUser)
        {
            var players = await PlayersOfTeam(currentUser.TeamId)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            // Players see their teammates, but not their email addresses
            return players.Select(p => ToDto(p, includeEmail: currentUser.IsCoach || currentUser.Id == p.Id)).ToList();
        }

        public async Task<RosterEntryDto?> GetPlayerAsync(CurrentUser currentUser, int playerId)
        {
            var player = await PlayersOfTeam(currentUser.TeamId).FirstOrDefaultAsync(u => u.Id == playerId);
            if (player == null)
            {
                return null;
            }

            return ToDto(player, includeEmail: currentUser.IsCoach || currentUser.Id == player.Id);
        }

        public async Task<TemporaryPasswordDto> CreatePlayerAsync(CurrentUser currentUser, CreatePlayerDto dto)
        {
            if (!AccessRules.CanManageTeam(currentUser))
            {
                throw new ForbiddenException("Only the head coach can add players.");
            }

            var email = dto.Email.Trim();
            await AccountRules.EnsureEmailIsUniqueAsync(_context, email);

            // Business rule: a player can only be put in groups of this team
            var groupIds = dto.GroupIds.Distinct().ToList();
            int groupsFound = await _context.PositionGroups
                .CountAsync(g => g.TeamId == currentUser.TeamId && groupIds.Contains(g.Id));
            if (groupsFound != groupIds.Count)
            {
                throw new InvalidOperationException("One of the chosen position groups does not exist on this team.");
            }

            var temporaryPassword = TemporaryPassword.Create();

            var player = new User
            {
                TeamId = currentUser.TeamId,
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Email = email,
                Role = UserRole.Player,
                CanPublishPlaybook = false,
                CanEditDepthChart = false,
                CanRecordAttendance = false,
                MustChangePassword = true,      // the temporary password must be replaced at first sign-in
                IsActive = true,
                CreatedAt = _clock.GetUtcNow().UtcDateTime,
                RosterEntry = new RosterEntry
                {
                    TeamId = currentUser.TeamId,
                    JerseyNumber = dto.JerseyNumber,
                    Grade = dto.Grade,
                    HeightInches = dto.HeightInches,
                    WeightLbs = dto.WeightLbs,
                    Status = RosterStatus.Active
                },
                PlayerGroupAssignments = groupIds
                    .Select(groupId => new PlayerGroupAssignment { PositionGroupId = groupId })
                    .ToList()
            };
            player.PasswordHash = _passwordHasher.HashPassword(player, temporaryPassword);

            _context.Users.Add(player);   // the roster entry and group assignments are saved with it
            await _context.SaveChangesAsync();

            return new TemporaryPasswordDto
            {
                UserId = player.Id,
                FirstName = player.FirstName,
                LastName = player.LastName,
                Email = player.Email,
                TemporaryPassword = temporaryPassword
            };
        }

        public async Task<bool> UpdateAsync(CurrentUser currentUser, int playerId, UpdateRosterEntryDto dto)
        {
            var player = await PlayersOfTeam(currentUser.TeamId).FirstOrDefaultAsync(u => u.Id == playerId);
            if (player == null)
            {
                return false;
            }

            // Permission: the head coach, or a coordinator when the player is in one of their unit's groups
            bool playerIsInCoordinatorsUnit = currentUser.IsCoordinator
                && player.PlayerGroupAssignments!.Any(a => a.PositionGroup!.Unit == currentUser.CoordinatorUnit);
            if (!currentUser.IsHeadCoach && !playerIsInCoordinatorsUnit)
            {
                throw new ForbiddenException("Only the head coach, or the coordinator of one of the player's units, can edit this roster entry.");
            }

            // A player created before roster entries existed gets one now
            player.RosterEntry ??= new RosterEntry { TeamId = currentUser.TeamId };

            player.RosterEntry.JerseyNumber = dto.JerseyNumber;
            player.RosterEntry.Grade = dto.Grade;
            player.RosterEntry.HeightInches = dto.HeightInches;
            player.RosterEntry.WeightLbs = dto.WeightLbs;
            player.RosterEntry.Status = dto.Status;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<CoachSummaryDto>?> GetCoachesAsync(CurrentUser currentUser, int playerId)
        {
            var player = await PlayersOfTeam(currentUser.TeamId).FirstOrDefaultAsync(u => u.Id == playerId);
            if (player == null)
            {
                return null;
            }

            // Permission: coaches can look this up for any player; a player only for themselves
            if (currentUser.IsPlayer && currentUser.Id != playerId)
            {
                throw new ForbiddenException("Players can only see their own coaches.");
            }

            var playerGroups = player.PlayerGroupAssignments!.Select(a => a.PositionGroup!).ToList();
            var playerGroupIds = playerGroups.Select(g => g.Id).ToList();
            var playerUnits = playerGroups.Select(g => g.Unit).Distinct().ToList();

            var coaches = await _context.Users
                .Where(u => u.TeamId == currentUser.TeamId && u.Role != UserRole.Player && u.IsActive)
                .Include(u => u.CoachGroupAssignments!)
                    .ThenInclude(a => a.PositionGroup)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.LastName)
                .ToListAsync();

            var result = new List<CoachSummaryDto>();
            foreach (var coach in coaches)
            {
                // A coach can coach this player for more than one reason; each reason becomes part of the title
                var titles = new List<string>();

                if (coach.Role == UserRole.HeadCoach)
                {
                    titles.Add("Head coach");
                }

                if (coach.Role == UserRole.Coordinator && coach.CoordinatorUnit != null && playerUnits.Contains(coach.CoordinatorUnit.Value))
                {
                    titles.Add($"{Labels.Unit(coach.CoordinatorUnit.Value)} coordinator");
                }

                var sharedGroups = coach.CoachGroupAssignments!
                    .Where(a => playerGroupIds.Contains(a.PositionGroupId))
                    .OrderBy(a => a.PositionGroup!.SortOrder)
                    .Select(a => a.PositionGroup!.Name)
                    .ToList();
                if (sharedGroups.Count > 0)
                {
                    titles.Add($"{string.Join(" and ", sharedGroups)} coach");
                }

                if (titles.Count > 0)
                {
                    result.Add(new CoachSummaryDto
                    {
                        Id = coach.Id,
                        FirstName = coach.FirstName,
                        LastName = coach.LastName,
                        Title = string.Join(", ", titles)
                    });
                }
            }

            return result;
        }

        // Players of one team, with their roster entry and groups loaded
        private IQueryable<User> PlayersOfTeam(int teamId)
        {
            return _context.Users
                .Where(u => u.TeamId == teamId && u.Role == UserRole.Player)
                .Include(u => u.RosterEntry)
                .Include(u => u.PlayerGroupAssignments!)
                    .ThenInclude(a => a.PositionGroup);
        }

        // Map entity → DTO
        private static RosterEntryDto ToDto(User player, bool includeEmail)
        {
            return new RosterEntryDto
            {
                PlayerId = player.Id,
                FirstName = player.FirstName,
                LastName = player.LastName,
                Email = includeEmail ? player.Email : null,
                JerseyNumber = player.RosterEntry?.JerseyNumber,
                Grade = player.RosterEntry?.Grade,
                HeightInches = player.RosterEntry?.HeightInches,
                WeightLbs = player.RosterEntry?.WeightLbs,
                Status = player.RosterEntry?.Status ?? RosterStatus.Active,
                IsActive = player.IsActive,
                Groups = (player.PlayerGroupAssignments ?? new List<PlayerGroupAssignment>())
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
    }
}
