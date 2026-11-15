using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class PositionGroupService : IPositionGroupService
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public PositionGroupService(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        // ---------- Groups ----------

        public async Task<List<PositionGroupDto>> GetGroupsAsync(CurrentUser currentUser)
        {
            var groups = await GroupsOfTeam(currentUser.TeamId)
                .OrderBy(g => g.Unit)
                .ThenBy(g => g.SortOrder)
                .ToListAsync();

            return groups.Select(g => ToDto(g, currentUser)).ToList();
        }

        public async Task<PositionGroupDto?> GetGroupAsync(CurrentUser currentUser, int id)
        {
            var group = await GroupsOfTeam(currentUser.TeamId).FirstOrDefaultAsync(g => g.Id == id);
            if (group == null)
            {
                return null;
            }

            return ToDto(group, currentUser);
        }

        public async Task<PositionGroupDto> CreateGroupAsync(CurrentUser currentUser, SavePositionGroupDto dto)
        {
            EnsureCanManageUnit(currentUser, dto.Unit);

            var name = dto.Name.Trim();
            await EnsureGroupNameIsUniqueAsync(currentUser.TeamId, dto.Unit, name);

            int lastSortOrder = await _context.PositionGroups
                .Where(g => g.TeamId == currentUser.TeamId)
                .MaxAsync(g => (int?)g.SortOrder) ?? 0;

            var group = new PositionGroup
            {
                TeamId = currentUser.TeamId,
                Unit = dto.Unit,
                Name = name,
                SortOrder = lastSortOrder + 1
            };

            _context.PositionGroups.Add(group);
            await _context.SaveChangesAsync();

            return (await GetGroupAsync(currentUser, group.Id))!;
        }

        public async Task<bool> RenameGroupAsync(CurrentUser currentUser, int id, SavePositionGroupDto dto)
        {
            var group = await _context.PositionGroups.FirstOrDefaultAsync(g => g.Id == id && g.TeamId == currentUser.TeamId);
            if (group == null)
            {
                return false;
            }

            EnsureCanManageUnit(currentUser, group.Unit);

            // The unit of a group never changes; only the name does
            var name = dto.Name.Trim();
            await EnsureGroupNameIsUniqueAsync(currentUser.TeamId, group.Unit, name, group.Id);

            group.Name = name;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteGroupAsync(CurrentUser currentUser, int id)
        {
            var group = await _context.PositionGroups
                .Include(g => g.Positions)
                .FirstOrDefaultAsync(g => g.Id == id && g.TeamId == currentUser.TeamId);
            if (group == null)
            {
                return false;
            }

            EnsureCanManageUnit(currentUser, group.Unit);

            // Business rule: a group that still has positions cannot be deleted
            if (group.Positions!.Count > 0)
            {
                throw new InvalidOperationException($"The {group.Name} group still has positions. Delete or move its positions first.");
            }

            _context.PositionGroups.Remove(group);   // its coach and player assignments are deleted with it
            await _context.SaveChangesAsync();
            return true;
        }

        // ---------- Who belongs to a group ----------

        public async Task<bool> SetCoachesAsync(CurrentUser currentUser, int id, SetGroupMembersDto dto)
        {
            var group = await _context.PositionGroups
                .Include(g => g.CoachAssignments)
                .FirstOrDefaultAsync(g => g.Id == id && g.TeamId == currentUser.TeamId);
            if (group == null)
            {
                return false;
            }

            if (!AccessRules.CanAssignCoaches(currentUser))
            {
                throw new ForbiddenException("Only the head coach can choose which coaches coach a group.");
            }

            var coachIds = dto.UserIds.Distinct().ToList();

            // Business rule: only active coaches of this team can coach a group
            int coachesFound = await _context.Users.CountAsync(u =>
                u.TeamId == currentUser.TeamId && u.Role != UserRole.Player && u.IsActive && coachIds.Contains(u.Id));
            if (coachesFound != coachIds.Count)
            {
                throw new InvalidOperationException("One of the chosen people is not an active coach on this team.");
            }

            // Remove the coaches who are no longer chosen, then add the new ones
            var assignments = group.CoachAssignments!;
            assignments.RemoveAll(a => !coachIds.Contains(a.CoachId));
            foreach (var coachId in coachIds)
            {
                if (!assignments.Any(a => a.CoachId == coachId))
                {
                    assignments.Add(new CoachGroupAssignment { CoachId = coachId });
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetPlayersAsync(CurrentUser currentUser, int id, SetGroupMembersDto dto)
        {
            var group = await _context.PositionGroups
                .Include(g => g.PlayerAssignments)
                .FirstOrDefaultAsync(g => g.Id == id && g.TeamId == currentUser.TeamId);
            if (group == null)
            {
                return false;
            }

            EnsureCanManageUnit(currentUser, group.Unit);

            var playerIds = dto.UserIds.Distinct().ToList();

            // Business rule: only active players of this team can be put in a group
            int playersFound = await _context.Users.CountAsync(u =>
                u.TeamId == currentUser.TeamId && u.Role == UserRole.Player && u.IsActive && playerIds.Contains(u.Id));
            if (playersFound != playerIds.Count)
            {
                throw new InvalidOperationException("One of the chosen people is not an active player on this team.");
            }

            // Business rule: a player who is on this week's or a later depth chart at one of the group's
            // positions has to be taken off that chart before leaving the group
            var removedPlayerIds = group.PlayerAssignments!
                .Where(a => !playerIds.Contains(a.PlayerId))
                .Select(a => a.PlayerId)
                .ToList();
            if (removedPlayerIds.Count > 0)
            {
                var thisMonday = WeekDates.MondayOf(WeekDates.Today(_clock));

                var stillPlaced = await _context.DepthChartPlacements
                    .Where(p => removedPlayerIds.Contains(p.PlayerId)
                        && p.Position!.PositionGroupId == group.Id
                        && p.DepthChartWeek!.WeekStartDate >= thisMonday)
                    .Select(p => new { p.Player!.FirstName, p.Player.LastName, p.Position!.Code })
                    .FirstOrDefaultAsync();
                if (stillPlaced != null)
                {
                    throw new InvalidOperationException(
                        $"{stillPlaced.FirstName} {stillPlaced.LastName} is on the depth chart at {stillPlaced.Code}. " +
                        "Remove the player from the depth chart before taking them out of the group.");
                }
            }

            // Remove the players who are no longer chosen, then add the new ones
            var assignments = group.PlayerAssignments!;
            assignments.RemoveAll(a => !playerIds.Contains(a.PlayerId));
            foreach (var playerId in playerIds)
            {
                if (!assignments.Any(a => a.PlayerId == playerId))
                {
                    assignments.Add(new PlayerGroupAssignment { PlayerId = playerId });
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // ---------- Positions ----------

        public async Task<PositionDto?> GetPositionAsync(CurrentUser currentUser, int id)
        {
            var position = await _context.Positions.FirstOrDefaultAsync(p => p.Id == id && p.TeamId == currentUser.TeamId);
            if (position == null)
            {
                return null;
            }

            return ToDto(position, currentUser);
        }

        public async Task<PositionDto> CreatePositionAsync(CurrentUser currentUser, SavePositionDto dto)
        {
            // Business rule: a position is created inside a group of this team.
            // A position a coach adds belongs to both games; only the standard positions are tied to one.
            var group = await _context.PositionGroups
                .FirstOrDefaultAsync(g => g.Id == dto.PositionGroupId && g.TeamId == currentUser.TeamId);
            if (group == null)
            {
                throw new InvalidOperationException("The position group does not exist on this team.");
            }

            EnsureCanManageUnit(currentUser, group.Unit);

            var code = dto.Code.Trim().ToUpperInvariant();
            await EnsurePositionCodeIsUniqueAsync(currentUser.TeamId, code);

            int lastSortOrder = await _context.Positions
                .Where(p => p.TeamId == currentUser.TeamId)
                .MaxAsync(p => (int?)p.SortOrder) ?? 0;

            var position = new Position
            {
                TeamId = currentUser.TeamId,
                PositionGroupId = group.Id,
                Code = code,
                Name = dto.Name.Trim(),
                SortOrder = lastSortOrder + 1
            };

            _context.Positions.Add(position);
            await _context.SaveChangesAsync();

            return ToDto(position, currentUser);
        }

        public async Task<bool> UpdatePositionAsync(CurrentUser currentUser, int id, SavePositionDto dto)
        {
            var position = await _context.Positions
                .Include(p => p.PositionGroup)
                .FirstOrDefaultAsync(p => p.Id == id && p.TeamId == currentUser.TeamId);
            if (position == null)
            {
                return false;
            }

            EnsureCanManageUnit(currentUser, position.PositionGroup!.Unit);

            // A position stays in its group; its code and name can change
            var code = dto.Code.Trim().ToUpperInvariant();
            await EnsurePositionCodeIsUniqueAsync(currentUser.TeamId, code, position.Id);

            position.Code = code;
            position.Name = dto.Name.Trim();

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePositionAsync(CurrentUser currentUser, int id)
        {
            var position = await _context.Positions
                .Include(p => p.PositionGroup)
                .FirstOrDefaultAsync(p => p.Id == id && p.TeamId == currentUser.TeamId);
            if (position == null)
            {
                return false;
            }

            EnsureCanManageUnit(currentUser, position.PositionGroup!.Unit);

            // Business rule: a position that appears on any week's depth chart cannot be deleted
            bool isUsed = await _context.DepthChartPlacements.AnyAsync(p => p.PositionId == position.Id);
            if (isUsed)
            {
                throw new InvalidOperationException($"{position.Code} has players on a depth chart, so it cannot be deleted.");
            }

            _context.Positions.Remove(position);
            await _context.SaveChangesAsync();
            return true;
        }

        // ---------- Helpers ----------

        // Groups of one team with their positions, coaches and players loaded
        private IQueryable<PositionGroup> GroupsOfTeam(int teamId)
        {
            return _context.PositionGroups
                .Where(g => g.TeamId == teamId)
                .Include(g => g.Positions)
                .Include(g => g.CoachAssignments!)
                    .ThenInclude(a => a.Coach)
                .Include(g => g.PlayerAssignments!)
                    .ThenInclude(a => a.Player)
                        .ThenInclude(u => u!.RosterEntry);
        }

        private static void EnsureCanManageUnit(CurrentUser currentUser, TeamUnit unit)
        {
            if (!AccessRules.CanManageUnit(currentUser, unit))
            {
                throw new ForbiddenException(
                    $"Only the head coach or the {Labels.Unit(unit).ToLowerInvariant()} coordinator can change {Labels.Unit(unit).ToLowerInvariant()} groups and positions.");
            }
        }

        // Business rule: two groups of the same unit cannot share a name
        private async Task EnsureGroupNameIsUniqueAsync(int teamId, TeamUnit unit, string name, int exceptGroupId = 0)
        {
            bool exists = await _context.PositionGroups
                .AnyAsync(g => g.TeamId == teamId && g.Unit == unit && g.Name == name && g.Id != exceptGroupId);
            if (exists)
            {
                throw new InvalidOperationException($"{Labels.Unit(unit)} already has a group called {name}.");
            }
        }

        // Business rule: a position code is used once per team, so "X" always means the same slot
        private async Task EnsurePositionCodeIsUniqueAsync(int teamId, string code, int exceptPositionId = 0)
        {
            bool exists = await _context.Positions
                .AnyAsync(p => p.TeamId == teamId && p.Code == code && p.Id != exceptPositionId);
            if (exists)
            {
                throw new InvalidOperationException($"The team already has a position with the code {code}.");
            }
        }

        // Map entity → DTO
        private static PositionGroupDto ToDto(PositionGroup group, CurrentUser currentUser)
        {
            return new PositionGroupDto
            {
                Id = group.Id,
                Unit = group.Unit,
                Name = group.Name,
                SortOrder = group.SortOrder,
                Positions = (group.Positions ?? new List<Position>())
                    .OrderBy(p => p.SortOrder)
                    .Select(p => ToDto(p, currentUser))
                    .ToList(),
                Coaches = (group.CoachAssignments ?? new List<CoachGroupAssignment>())
                    .Where(a => a.Coach != null)
                    .OrderBy(a => a.Coach!.LastName)
                    .Select(a => new PersonDto
                    {
                        Id = a.CoachId,
                        FirstName = a.Coach!.FirstName,
                        LastName = a.Coach.LastName
                    })
                    .ToList(),
                Players = (group.PlayerAssignments ?? new List<PlayerGroupAssignment>())
                    .Where(a => a.Player != null)
                    .OrderBy(a => a.Player!.LastName)
                    .ThenBy(a => a.Player!.FirstName)
                    .Select(a => new PersonDto
                    {
                        Id = a.PlayerId,
                        FirstName = a.Player!.FirstName,
                        LastName = a.Player.LastName,
                        JerseyNumber = a.Player.RosterEntry?.JerseyNumber
                    })
                    .ToList(),
                CanManage = AccessRules.CanManageUnit(currentUser, group.Unit),
                CanAssignCoaches = AccessRules.CanAssignCoaches(currentUser)
            };
        }

        private static PositionDto ToDto(Position position, CurrentUser currentUser)
        {
            return new PositionDto
            {
                Id = position.Id,
                PositionGroupId = position.PositionGroupId,
                Code = position.Code,
                Name = position.Name,
                SortOrder = position.SortOrder,
                Rules = position.Rules,

                // A position only the other game has is left off the depth chart while the team plays this one
                IsPutAway = position.Rules != null && position.Rules != currentUser.Rules
            };
        }
    }
}
