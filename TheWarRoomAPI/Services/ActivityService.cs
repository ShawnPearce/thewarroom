using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class ActivityService : IActivityService
    {
        public const int MaxWeeks = 26;

        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public ActivityService(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        public async Task<ActivityReportDto> GetWeekAsync(CurrentUser currentUser, DateOnly? weekOf)
        {
            if (!currentUser.IsCoach)
            {
                throw new ForbiddenException("The team's activity report is for coaches. Players see their own activity.");
            }

            var thisMonday = WeekDates.MondayOf(WeekDates.Today(_clock));
            var monday = weekOf != null ? WeekDates.MondayOf(weekOf.Value) : thisMonday;

            // Business rule: there is nothing to report on a week that has not begun, so it gives the current week
            if (monday > thisMonday)
            {
                monday = thisMonday;
            }

            // The active players of the team, with their groups
            var players = await _context.Users
                .Where(u => u.TeamId == currentUser.TeamId && u.Role == UserRole.Player && u.IsActive)
                .Include(u => u.RosterEntry)
                .Include(u => u.PlayerGroupAssignments!)
                    .ThenInclude(a => a.PositionGroup)
                .ToListAsync();

            // Permission: the head coach sees every player; another coach sees the players of the groups in their scope
            players = players
                .Where(p => AccessRules.CanSeeActivityOf(currentUser, p.Id, GroupsOf(p)))
                .OrderBy(p => p.RosterEntry?.JerseyNumber ?? int.MaxValue)
                .ThenBy(p => p.LastName)
                .ToList();

            var times = await TimesByWeekAsync(players.Select(p => p.Id).ToList(), monday, monday.AddDays(7));

            return new ActivityReportDto
            {
                WeekStart = monday,
                Players = players
                    .Select(p =>
                    {
                        var time = times.GetValueOrDefault((p.Id, monday)) ?? new StudyTimeDto();
                        return new ActivityRowDto
                        {
                            PlayerId = p.Id,
                            FirstName = p.FirstName,
                            LastName = p.LastName,
                            JerseyNumber = p.RosterEntry?.JerseyNumber,
                            Groups = GroupNamesOf(p),
                            LoginSeconds = time.LoginSeconds,
                            FilmSeconds = time.FilmSeconds,
                            PlaybookSeconds = time.PlaybookSeconds,
                            SignIns = time.SignIns
                        };
                    })
                    .ToList()
            };
        }

        public async Task<PlayerActivityDto?> GetPlayerAsync(CurrentUser currentUser, int playerId, int weeks)
        {
            var player = await _context.Users
                .Where(u => u.Id == playerId && u.TeamId == currentUser.TeamId && u.Role == UserRole.Player)
                .Include(u => u.RosterEntry)
                .Include(u => u.PlayerGroupAssignments!)
                    .ThenInclude(a => a.PositionGroup)
                .FirstOrDefaultAsync();
            if (player == null)
            {
                return null;
            }

            // Permission: players see their own activity; coaches see the players in their scope
            if (!AccessRules.CanSeeActivityOf(currentUser, player.Id, GroupsOf(player)))
            {
                throw new ForbiddenException(currentUser.IsCoach
                    ? "This player is not in one of your groups."
                    : "You can see your own activity only.");
            }

            weeks = Math.Clamp(weeks, 1, MaxWeeks);
            var thisMonday = WeekDates.MondayOf(WeekDates.Today(_clock));
            var firstMonday = thisMonday.AddDays(-7 * (weeks - 1));

            var times = await TimesByWeekAsync(new List<int> { player.Id }, firstMonday, thisMonday.AddDays(7));

            var result = new PlayerActivityDto
            {
                PlayerId = player.Id,
                FirstName = player.FirstName,
                LastName = player.LastName,
                JerseyNumber = player.RosterEntry?.JerseyNumber,
                Groups = GroupNamesOf(player)
            };

            // Every week is listed, also the ones with nothing in them, newest first
            for (var monday = thisMonday; monday >= firstMonday; monday = monday.AddDays(-7))
            {
                var time = times.GetValueOrDefault((player.Id, monday)) ?? new StudyTimeDto();
                result.Weeks.Add(new ActivityWeekDto
                {
                    WeekStart = monday,
                    LoginSeconds = time.LoginSeconds,
                    FilmSeconds = time.FilmSeconds,
                    PlaybookSeconds = time.PlaybookSeconds,
                    SignIns = time.SignIns
                });
            }

            return result;
        }

        // ---------- Helpers ----------

        // Adds up login time and viewing time for the given players, for each week from fromMonday up to (not including) toMonday.
        // A sign-in or a page visit belongs to the week it started in.
        private async Task<Dictionary<(int PlayerId, DateOnly Monday), StudyTimeDto>> TimesByWeekAsync(
            List<int> playerIds, DateOnly fromMonday, DateOnly toMonday)
        {
            var fromUtc = StartOfDayUtc(fromMonday);
            var toUtc = StartOfDayUtc(toMonday);

            var logins = await _context.LoginSessions
                .Where(s => playerIds.Contains(s.UserId) && s.StartedAt >= fromUtc && s.StartedAt < toUtc)
                .Select(s => new { s.UserId, s.StartedAt, s.LastSeenAt })
                .ToListAsync();

            var viewings = await _context.ViewingSessions
                .Where(v => playerIds.Contains(v.UserId) && v.StartedAt >= fromUtc && v.StartedAt < toUtc)
                .Select(v => new { v.UserId, v.StartedAt, v.Kind, v.ActiveMs })
                .ToListAsync();

            var totals = new Dictionary<(int, DateOnly), StudyTimeDto>();

            StudyTimeDto TotalFor(int playerId, DateTime startedAtUtc)
            {
                var key = (playerId, WeekOf(startedAtUtc));
                if (!totals.TryGetValue(key, out var total))
                {
                    total = new StudyTimeDto();
                    totals[key] = total;
                }
                return total;
            }

            // Business rule: login time runs from the sign-in to the last request of that session.
            // It is its own number and is never mixed with viewing time.
            foreach (var login in logins)
            {
                var total = TotalFor(login.UserId, login.StartedAt);
                total.LoginSeconds += (int)Math.Max(0, (login.LastSeenAt - login.StartedAt).TotalSeconds);
                total.SignIns++;
            }

            // Business rule: film time and playbook time are counted separately
            var filmMs = new Dictionary<(int, DateOnly), long>();
            var playbookMs = new Dictionary<(int, DateOnly), long>();
            foreach (var viewing in viewings)
            {
                TotalFor(viewing.UserId, viewing.StartedAt);   // makes sure the week has a row

                var key = (viewing.UserId, WeekOf(viewing.StartedAt));
                var bucket = viewing.Kind == ViewingKind.Film ? filmMs : playbookMs;
                bucket[key] = bucket.GetValueOrDefault(key) + viewing.ActiveMs;
            }

            foreach (var entry in totals)
            {
                entry.Value.FilmSeconds = (int)(filmMs.GetValueOrDefault(entry.Key) / 1000);
                entry.Value.PlaybookSeconds = (int)(playbookMs.GetValueOrDefault(entry.Key) / 1000);
            }

            return totals;
        }

        // The Monday of the week a moment falls in, by the clock on the wall where the team is
        private DateOnly WeekOf(DateTime utc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _clock.LocalTimeZone);
            return WeekDates.MondayOf(DateOnly.FromDateTime(local));
        }

        private DateTime StartOfDayUtc(DateOnly day)
        {
            var localMidnight = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(localMidnight, _clock.LocalTimeZone);
        }

        private static List<PositionGroup> GroupsOf(User player)
        {
            return player.PlayerGroupAssignments!.Select(a => a.PositionGroup!).ToList();
        }

        private static List<string> GroupNamesOf(User player)
        {
            return player.PlayerGroupAssignments!
                .Select(a => a.PositionGroup!)
                .OrderBy(g => g.SortOrder)
                .Select(g => g.Name)
                .ToList();
        }
    }
}
