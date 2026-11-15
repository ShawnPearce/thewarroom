using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.Diagrams;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class DepthChartService : IDepthChartService
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public DepthChartService(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        // ---------- Weeks ----------

        public async Task<List<DepthChartWeekDto>> GetWeeksAsync(CurrentUser currentUser)
        {
            return await _context.DepthChartWeeks
                .Where(w => w.TeamId == currentUser.TeamId)
                .OrderByDescending(w => w.WeekStartDate)
                .Select(w => new DepthChartWeekDto
                {
                    Id = w.Id,
                    WeekStartDate = w.WeekStartDate,
                    Label = w.Label
                })
                .ToListAsync();
        }

        public async Task<DepthChartWeekDto> CreateWeekAsync(CurrentUser currentUser, CreateDepthChartWeekDto dto)
        {
            if (!AccessRules.CanCreateDepthChartWeek(currentUser))
            {
                throw new ForbiddenException("Only the head coach and coordinators can start a new week's depth chart.");
            }

            // Whatever day was chosen, the week is stored by its Monday
            var monday = WeekDates.MondayOf(dto.WeekStartDate);

            // Business rule: one depth chart per week
            bool exists = await _context.DepthChartWeeks
                .AnyAsync(w => w.TeamId == currentUser.TeamId && w.WeekStartDate == monday);
            if (exists)
            {
                throw new InvalidOperationException($"There is already a depth chart for the week of {monday:MMMM d, yyyy}.");
            }

            var week = new DepthChartWeek
            {
                TeamId = currentUser.TeamId,
                WeekStartDate = monday,
                Label = string.IsNullOrWhiteSpace(dto.Label) ? null : dto.Label.Trim(),
                CreatedAt = _clock.GetUtcNow().UtcDateTime,
                Placements = new List<DepthChartPlacement>()
            };

            if (dto.CopyFromWeekId != null)
            {
                await CopyPlacementsAsync(currentUser, dto.CopyFromWeekId.Value, week);
            }

            _context.DepthChartWeeks.Add(week);
            await _context.SaveChangesAsync();

            return new DepthChartWeekDto { Id = week.Id, WeekStartDate = week.WeekStartDate, Label = week.Label };
        }

        public async Task<bool> DeleteWeekAsync(CurrentUser currentUser, int weekId)
        {
            var week = await FindWeekAsync(currentUser, weekId);
            if (week == null)
            {
                return false;
            }

            if (!AccessRules.CanManageTeam(currentUser))
            {
                throw new ForbiddenException("Only the head coach can delete a week's depth chart.");
            }

            _context.DepthChartWeeks.Remove(week);   // its placements are deleted with it
            await _context.SaveChangesAsync();
            return true;
        }

        // ---------- Reading the chart ----------

        public async Task<DepthChartDto?> GetWeekAsync(CurrentUser currentUser, int weekId)
        {
            var week = await FindWeekAsync(currentUser, weekId);
            if (week == null)
            {
                return null;
            }

            var groups = await _context.PositionGroups
                .Where(g => g.TeamId == currentUser.TeamId)
                .Include(g => g.Positions)
                .OrderBy(g => g.Unit)
                .ThenBy(g => g.SortOrder)
                .ToListAsync();

            var placements = await _context.DepthChartPlacements
                .Where(p => p.DepthChartWeekId == week.Id)
                .Include(p => p.Player)
                    .ThenInclude(u => u!.RosterEntry)
                .OrderBy(p => p.Rank)
                .ToListAsync();

            // Business rule: the chart shows the positions of the game the team plays. A position only the other
            // game has is put away with the players listed at it. A group whose positions are all put away
            // (the tight ends, in Canadian football) is left off the chart too.
            groups = groups
                .Where(group => group.Positions!.Count == 0 || group.Positions.Any(position => IsInUse(currentUser, position)))
                .ToList();

            return new DepthChartDto
            {
                Week = ToDto(week),
                Rules = currentUser.Rules,
                PlayersPerSide = DiagramValidator.PlayersPerSide(currentUser.Rules),
                CanCreateWeek = AccessRules.CanCreateDepthChartWeek(currentUser),
                CanDeleteWeek = AccessRules.CanManageTeam(currentUser),
                Groups = groups.Select(group => new DepthChartGroupDto
                {
                    GroupId = group.Id,
                    GroupName = group.Name,
                    Unit = group.Unit,
                    CanEdit = AccessRules.CanEditDepthChart(currentUser, group),
                    Positions = group.Positions!
                        .Where(position => IsInUse(currentUser, position))
                        .OrderBy(position => position.SortOrder)
                        .Select(position => new DepthChartPositionDto
                        {
                            PositionId = position.Id,
                            Code = position.Code,
                            Name = position.Name,
                            Players = placements
                                .Where(p => p.PositionId == position.Id)
                                .Select(ToDto)
                                .ToList()
                        })
                        .ToList()
                }).ToList()
            };
        }

        public async Task<DepthChartDto?> GetCurrentWeekAsync(CurrentUser currentUser)
        {
            var thisMonday = WeekDates.MondayOf(WeekDates.Today(_clock));

            // This week's chart if there is one, otherwise the most recent earlier week...
            var week = await _context.DepthChartWeeks
                .Where(w => w.TeamId == currentUser.TeamId && w.WeekStartDate <= thisMonday)
                .OrderByDescending(w => w.WeekStartDate)
                .FirstOrDefaultAsync();

            // ...and if the team only has future weeks, the first of those
            week ??= await _context.DepthChartWeeks
                .Where(w => w.TeamId == currentUser.TeamId)
                .OrderBy(w => w.WeekStartDate)
                .FirstOrDefaultAsync();

            if (week == null)
            {
                return null;
            }

            return await GetWeekAsync(currentUser, week.Id);
        }

        public async Task<DepthChartSlotDto?> GetSlotAsync(CurrentUser currentUser, int weekId, int positionId)
        {
            var week = await FindWeekAsync(currentUser, weekId);
            if (week == null)
            {
                return null;
            }

            var position = await FindPositionAsync(currentUser, positionId);
            if (position == null)
            {
                return null;
            }

            var placements = await _context.DepthChartPlacements
                .Where(p => p.DepthChartWeekId == week.Id && p.PositionId == position.Id)
                .Include(p => p.Player)
                    .ThenInclude(u => u!.RosterEntry)
                .OrderBy(p => p.Rank)
                .ToListAsync();

            var placedPlayerIds = placements.Select(p => p.PlayerId).ToList();

            // Players who can still be added: active members of the position's group who are not listed yet
            var eligiblePlayers = await _context.PlayerGroupAssignments
                .Where(a => a.PositionGroupId == position.PositionGroupId
                    && a.Player!.IsActive
                    && (a.Player.RosterEntry == null || a.Player.RosterEntry.Status != RosterStatus.Inactive)
                    && !placedPlayerIds.Contains(a.PlayerId))
                .OrderBy(a => a.Player!.LastName)
                .ThenBy(a => a.Player!.FirstName)
                .Select(a => new PersonDto
                {
                    Id = a.PlayerId,
                    FirstName = a.Player!.FirstName,
                    LastName = a.Player.LastName,
                    JerseyNumber = a.Player.RosterEntry != null ? a.Player.RosterEntry.JerseyNumber : null
                })
                .ToListAsync();

            return new DepthChartSlotDto
            {
                Week = ToDto(week),
                PositionId = position.Id,
                Code = position.Code,
                Name = position.Name,
                GroupId = position.PositionGroupId,
                GroupName = position.PositionGroup!.Name,
                Unit = position.PositionGroup.Unit,
                CanEdit = AccessRules.CanEditDepthChart(currentUser, position.PositionGroup),
                Players = placements.Select(ToDto).ToList(),
                EligiblePlayers = eligiblePlayers
            };
        }

        public async Task<List<MyPlacementDto>?> GetMyPlacementsAsync(CurrentUser currentUser, int weekId)
        {
            var week = await FindWeekAsync(currentUser, weekId);
            if (week == null)
            {
                return null;
            }

            // Only at the positions of the game the team plays (see GetWeekAsync)
            return await _context.DepthChartPlacements
                .Where(p => p.DepthChartWeekId == week.Id && p.PlayerId == currentUser.Id)
                .Where(p => p.Position!.Rules == null || p.Position.Rules == currentUser.Rules)
                .OrderBy(p => p.Position!.SortOrder)
                .Select(p => new MyPlacementDto
                {
                    WeekId = p.DepthChartWeekId,
                    PositionId = p.PositionId,
                    Code = p.Position!.Code,
                    PositionName = p.Position.Name,
                    GroupName = p.Position.PositionGroup!.Name,
                    Unit = p.Position.PositionGroup.Unit,
                    Rank = p.Rank,
                    PlayersAtPosition = _context.DepthChartPlacements
                        .Count(other => other.DepthChartWeekId == p.DepthChartWeekId && other.PositionId == p.PositionId)
                })
                .ToListAsync();
        }

        // ---------- Changing the chart ----------

        public async Task<DepthChartSlotDto?> AddPlayerAsync(CurrentUser currentUser, int weekId, int positionId, AddPlacementDto dto)
        {
            var week = await FindWeekAsync(currentUser, weekId);
            if (week == null)
            {
                return null;
            }

            var position = await FindPositionAsync(currentUser, positionId);
            if (position == null)
            {
                return null;
            }

            EnsureCanEdit(currentUser, position.PositionGroup!);

            // Business rule: only a player of this team can be placed
            var player = await _context.Users
                .Include(u => u.RosterEntry)
                .FirstOrDefaultAsync(u => u.Id == dto.PlayerId && u.TeamId == currentUser.TeamId && u.Role == UserRole.Player);
            if (player == null)
            {
                throw new InvalidOperationException("That player is not on this team.");
            }

            var playerName = Labels.FullName(player);

            // Business rule: the player must be active
            if (!player.IsActive || player.RosterEntry?.Status == RosterStatus.Inactive)
            {
                throw new InvalidOperationException($"{playerName} is not active and cannot be put on the depth chart.");
            }

            // Business rule: the player must belong to the position's group
            bool isInGroup = await _context.PlayerGroupAssignments
                .AnyAsync(a => a.PlayerId == player.Id && a.PositionGroupId == position.PositionGroupId);
            if (!isInGroup)
            {
                throw new InvalidOperationException(
                    $"{playerName} is not in the {position.PositionGroup!.Name} group. Add the player to the group first.");
            }

            var slot = await LoadSlotAsync(week.Id, position.Id);

            // Business rule: a player appears once at a position (but may appear at several positions)
            if (slot.Any(p => p.PlayerId == player.Id))
            {
                throw new InvalidOperationException($"{playerName} is already listed at {position.Code} this week.");
            }

            // A new player goes to the bottom of the list
            _context.DepthChartPlacements.Add(new DepthChartPlacement
            {
                TeamId = currentUser.TeamId,
                DepthChartWeekId = week.Id,
                PositionId = position.Id,
                PlayerId = player.Id,
                Rank = slot.Count + 1
            });
            await _context.SaveChangesAsync();

            return await GetSlotAsync(currentUser, week.Id, position.Id);
        }

        public async Task<bool> MovePlacementAsync(CurrentUser currentUser, int placementId, MoveDirection direction)
        {
            var placement = await FindPlacementAsync(currentUser, placementId);
            if (placement == null)
            {
                return false;
            }

            EnsureCanEdit(currentUser, placement.Position!.PositionGroup!);

            var slot = await LoadSlotAsync(placement.DepthChartWeekId, placement.PositionId);

            // Swap the player with the neighbour above or below.
            // Moving the starter up, or the last player down, changes nothing.
            int index = slot.FindIndex(p => p.Id == placement.Id);
            int neighbour = direction == MoveDirection.Up ? index - 1 : index + 1;
            if (neighbour >= 0 && neighbour < slot.Count)
            {
                (slot[index], slot[neighbour]) = (slot[neighbour], slot[index]);
            }

            Renumber(slot);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemovePlacementAsync(CurrentUser currentUser, int placementId)
        {
            var placement = await FindPlacementAsync(currentUser, placementId);
            if (placement == null)
            {
                return false;
            }

            EnsureCanEdit(currentUser, placement.Position!.PositionGroup!);

            var slot = await LoadSlotAsync(placement.DepthChartWeekId, placement.PositionId);
            slot.RemoveAll(p => p.Id == placement.Id);
            _context.DepthChartPlacements.Remove(placement);

            // The players below move up, so the ranks stay 1, 2, 3...
            Renumber(slot);
            await _context.SaveChangesAsync();
            return true;
        }

        // ---------- Helpers ----------

        // Every lookup by id also checks the team, so one team can never reach another team's records
        private async Task<DepthChartWeek?> FindWeekAsync(CurrentUser currentUser, int weekId)
        {
            return await _context.DepthChartWeeks
                .FirstOrDefaultAsync(w => w.Id == weekId && w.TeamId == currentUser.TeamId);
        }

        // A position that is put away is not found: nothing at it can be looked at or changed
        // until the team plays its game again.
        private async Task<Position?> FindPositionAsync(CurrentUser currentUser, int positionId)
        {
            return await _context.Positions
                .Include(p => p.PositionGroup)
                .FirstOrDefaultAsync(p => p.Id == positionId && p.TeamId == currentUser.TeamId
                    && (p.Rules == null || p.Rules == currentUser.Rules));
        }

        private async Task<DepthChartPlacement?> FindPlacementAsync(CurrentUser currentUser, int placementId)
        {
            return await _context.DepthChartPlacements
                .Include(p => p.Position)
                    .ThenInclude(position => position!.PositionGroup)
                .FirstOrDefaultAsync(p => p.Id == placementId && p.TeamId == currentUser.TeamId
                    && (p.Position!.Rules == null || p.Position.Rules == currentUser.Rules));
        }

        // Does the game the team plays have this position? Most positions belong to both games.
        private static bool IsInUse(CurrentUser currentUser, Position position)
        {
            return position.Rules == null || position.Rules == currentUser.Rules;
        }

        // The placements at one position in one week, starter first
        private async Task<List<DepthChartPlacement>> LoadSlotAsync(int weekId, int positionId)
        {
            return await _context.DepthChartPlacements
                .Where(p => p.DepthChartWeekId == weekId && p.PositionId == positionId)
                .OrderBy(p => p.Rank)
                .ThenBy(p => p.Id)
                .ToListAsync();
        }

        // Ranks are always stored as 1, 2, 3... with no gaps
        private static void Renumber(List<DepthChartPlacement> slot)
        {
            for (int i = 0; i < slot.Count; i++)
            {
                slot[i].Rank = i + 1;
            }
        }

        private static void EnsureCanEdit(CurrentUser currentUser, PositionGroup group)
        {
            if (AccessRules.CanEditDepthChart(currentUser, group))
            {
                return;
            }

            if (!currentUser.IsCoach)
            {
                throw new ForbiddenException("Only coaches can change the depth chart.");
            }

            if (!currentUser.CanEditDepthChart)
            {
                throw new ForbiddenException("The head coach has switched off depth chart editing for your account.");
            }

            throw new ForbiddenException($"The {group.Name} group is outside the groups you coach.");
        }

        // Starts a new week as a copy of an earlier one.
        // Players who have since been deactivated, or who have left the position's group, are not carried over.
        // Neither is anyone listed at a position that is put away: the new week is made for the game the team plays now.
        private async Task CopyPlacementsAsync(CurrentUser currentUser, int sourceWeekId, DepthChartWeek newWeek)
        {
            var source = await FindWeekAsync(currentUser, sourceWeekId);
            if (source == null)
            {
                throw new InvalidOperationException("The week to copy from does not exist on this team.");
            }

            var sourcePlacements = await _context.DepthChartPlacements
                .Where(p => p.DepthChartWeekId == source.Id)
                .Where(p => p.Position!.Rules == null || p.Position.Rules == currentUser.Rules)
                .Include(p => p.Position)
                .Include(p => p.Player)
                    .ThenInclude(u => u!.RosterEntry)
                .OrderBy(p => p.PositionId)
                .ThenBy(p => p.Rank)
                .ToListAsync();

            var memberships = await _context.PlayerGroupAssignments
                .Where(a => a.PositionGroup!.TeamId == currentUser.TeamId)
                .Select(a => new { a.PlayerId, a.PositionGroupId })
                .ToListAsync();

            foreach (var positionPlacements in sourcePlacements.GroupBy(p => p.PositionId))
            {
                int rank = 0;
                foreach (var placement in positionPlacements)
                {
                    var player = placement.Player!;
                    bool isActive = player.IsActive && player.RosterEntry?.Status != RosterStatus.Inactive;
                    bool isInGroup = memberships.Any(m =>
                        m.PlayerId == player.Id && m.PositionGroupId == placement.Position!.PositionGroupId);

                    if (isActive && isInGroup)
                    {
                        newWeek.Placements!.Add(new DepthChartPlacement
                        {
                            TeamId = currentUser.TeamId,
                            PositionId = placement.PositionId,
                            PlayerId = player.Id,
                            Rank = ++rank
                        });
                    }
                }
            }
        }

        // Map entity → DTO
        private static DepthChartWeekDto ToDto(DepthChartWeek week)
        {
            return new DepthChartWeekDto
            {
                Id = week.Id,
                WeekStartDate = week.WeekStartDate,
                Label = week.Label
            };
        }

        private static DepthChartPlayerDto ToDto(DepthChartPlacement placement)
        {
            return new DepthChartPlayerDto
            {
                PlacementId = placement.Id,
                PlayerId = placement.PlayerId,
                FirstName = placement.Player!.FirstName,
                LastName = placement.Player.LastName,
                JerseyNumber = placement.Player.RosterEntry?.JerseyNumber,
                Rank = placement.Rank,
                Status = placement.Player.RosterEntry?.Status ?? RosterStatus.Active,
                IsActive = placement.Player.IsActive
            };
        }
    }
}
