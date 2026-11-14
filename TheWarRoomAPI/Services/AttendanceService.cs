using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly AppDbContext _context;
        private readonly IScheduleService _scheduleService;
        private readonly TimeProvider _clock;

        public AttendanceService(AppDbContext context, IScheduleService scheduleService, TimeProvider clock)
        {
            _context = context;
            _scheduleService = scheduleService;
            _clock = clock;
        }

        public async Task<AttendanceSheetDto?> GetSheetAsync(CurrentUser currentUser, int eventId)
        {
            var scheduledEvent = await _scheduleService.GetEventAsync(currentUser, eventId);   // also checks the team
            if (scheduledEvent == null)
            {
                return null;
            }

            // Permission: the full sheet is for coaches; a player sees only their own record
            if (!currentUser.IsCoach)
            {
                throw new ForbiddenException("Only coaches can see an event's attendance sheet.");
            }

            bool trackingEnabled = await IsTrackingEnabledAsync(currentUser.TeamId);

            var records = await _context.AttendanceRecords
                .Where(a => a.ScheduledEventId == eventId)
                .Include(a => a.RecordedBy)
                .ToListAsync();
            var recordedPlayerIds = records.Select(r => r.PlayerId).ToList();

            // The sheet lists every active player, plus anyone who already has a record for this event
            var players = await _context.Users
                .Where(u => u.TeamId == currentUser.TeamId && u.Role == UserRole.Player)
                .Where(u => recordedPlayerIds.Contains(u.Id)
                    || (u.IsActive && (u.RosterEntry == null || u.RosterEntry.Status != RosterStatus.Inactive)))
                .Include(u => u.RosterEntry)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            string? reason = WhyRecordingIsNotAllowed(currentUser, trackingEnabled, scheduledEvent.EventDate);

            return new AttendanceSheetDto
            {
                Event = scheduledEvent,
                TrackingEnabled = trackingEnabled,
                CanRecord = reason == null,
                CannotRecordReason = reason,
                Rows = players.Select(player =>
                {
                    var record = records.FirstOrDefault(r => r.PlayerId == player.Id);
                    return new AttendanceRowDto
                    {
                        PlayerId = player.Id,
                        FirstName = player.FirstName,
                        LastName = player.LastName,
                        JerseyNumber = player.RosterEntry?.JerseyNumber,
                        Status = record?.Status,
                        RecordedBy = record?.RecordedBy != null ? Labels.FullName(record.RecordedBy) : null,
                        RecordedAt = record?.RecordedAt
                    };
                }).ToList()
            };
        }

        public async Task<bool> RecordAsync(CurrentUser currentUser, int eventId, RecordAttendanceDto dto)
        {
            var scheduledEvent = await _context.ScheduledEvents
                .FirstOrDefaultAsync(e => e.Id == eventId && e.TeamId == currentUser.TeamId);
            if (scheduledEvent == null)
            {
                return false;
            }

            // Permission: only coaches, and only those whose attendance permission is switched on
            if (!AccessRules.CanRecordAttendance(currentUser))
            {
                throw new ForbiddenException(currentUser.IsCoach
                    ? "The head coach has switched off attendance recording for your account."
                    : "Only coaches can record attendance.");
            }

            // Business rule: attendance can be recorded only while the head coach has tracking switched on.
            // This applies to everyone, the head coach included.
            bool trackingEnabled = await IsTrackingEnabledAsync(currentUser.TeamId);
            if (!trackingEnabled)
            {
                throw new InvalidOperationException("Attendance tracking is switched off for this team. The head coach can switch it on in Team settings.");
            }

            // Business rule: attendance is taken on the day of the event or afterwards, not in advance
            if (scheduledEvent.EventDate > WeekDates.Today(_clock))
            {
                throw new InvalidOperationException("Attendance can be recorded from the day of the event.");
            }

            // Business rule: every entry must be about a player of this team, and a player appears once
            var playerIds = dto.Entries.Select(e => e.PlayerId).ToList();
            if (playerIds.Count != playerIds.Distinct().Count())
            {
                throw new InvalidOperationException("A player appears more than once on the sheet.");
            }

            int playersFound = await _context.Users.CountAsync(u =>
                u.TeamId == currentUser.TeamId && u.Role == UserRole.Player && playerIds.Contains(u.Id));
            if (playersFound != playerIds.Count)
            {
                throw new InvalidOperationException("One of the entries is not a player on this team.");
            }

            var existingRecords = await _context.AttendanceRecords
                .Where(a => a.ScheduledEventId == eventId)
                .ToListAsync();
            var now = _clock.GetUtcNow().UtcDateTime;

            foreach (var entry in dto.Entries)
            {
                var record = existingRecords.FirstOrDefault(r => r.PlayerId == entry.PlayerId);

                if (entry.Status == null)
                {
                    // An empty status removes what was recorded
                    if (record != null)
                    {
                        _context.AttendanceRecords.Remove(record);
                    }
                }
                else if (record == null)
                {
                    _context.AttendanceRecords.Add(new AttendanceRecord
                    {
                        TeamId = currentUser.TeamId,
                        ScheduledEventId = eventId,
                        PlayerId = entry.PlayerId,
                        Status = entry.Status.Value,
                        RecordedById = currentUser.Id,
                        RecordedAt = now
                    });
                }
                else if (record.Status != entry.Status.Value)
                {
                    // Only a real change replaces who recorded it and when
                    record.Status = entry.Status.Value;
                    record.RecordedById = currentUser.Id;
                    record.RecordedAt = now;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<MyAttendanceDto>> GetMyAttendanceAsync(CurrentUser currentUser)
        {
            return await _context.AttendanceRecords
                .Where(a => a.PlayerId == currentUser.Id && a.TeamId == currentUser.TeamId)
                .OrderByDescending(a => a.ScheduledEvent!.EventDate)
                .ThenByDescending(a => a.ScheduledEvent!.StartTime)
                .Select(a => new MyAttendanceDto
                {
                    EventId = a.ScheduledEventId,
                    EventType = a.ScheduledEvent!.EventType,
                    Title = a.ScheduledEvent.Title,
                    EventDate = a.ScheduledEvent.EventDate,
                    StartTime = a.ScheduledEvent.StartTime,
                    Status = a.Status
                })
                .ToListAsync();
        }

        public async Task<List<AttendanceSummaryDto>> GetSummaryAsync(CurrentUser currentUser, DateOnly? from, DateOnly? to)
        {
            // Permission: the team-wide summary is for coaches
            if (!currentUser.IsCoach)
            {
                throw new ForbiddenException("Only coaches can see the team's attendance summary.");
            }

            var records = _context.AttendanceRecords.Where(a => a.TeamId == currentUser.TeamId);
            if (from != null)
            {
                records = records.Where(a => a.ScheduledEvent!.EventDate >= from);
            }
            if (to != null)
            {
                records = records.Where(a => a.ScheduledEvent!.EventDate <= to);
            }

            var statuses = await records
                .Select(a => new { a.PlayerId, a.Status })
                .ToListAsync();

            var players = await _context.Users
                .Where(u => u.TeamId == currentUser.TeamId && u.Role == UserRole.Player && u.IsActive)
                .Include(u => u.RosterEntry)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            // Every active player is listed, including those with nothing recorded yet
            return players.Select(player =>
            {
                var own = statuses.Where(s => s.PlayerId == player.Id).ToList();
                return new AttendanceSummaryDto
                {
                    PlayerId = player.Id,
                    FirstName = player.FirstName,
                    LastName = player.LastName,
                    JerseyNumber = player.RosterEntry?.JerseyNumber,
                    Present = own.Count(s => s.Status == AttendanceStatus.Present),
                    Late = own.Count(s => s.Status == AttendanceStatus.Late),
                    Absent = own.Count(s => s.Status == AttendanceStatus.Absent),
                    Excused = own.Count(s => s.Status == AttendanceStatus.Excused)
                };
            }).ToList();
        }

        private async Task<bool> IsTrackingEnabledAsync(int teamId)
        {
            return await _context.Teams
                .Where(t => t.Id == teamId)
                .Select(t => t.AttendanceTrackingEnabled)
                .FirstAsync();
        }

        // The same three rules RecordAsync enforces, worded for the page. Null means recording is allowed.
        private string? WhyRecordingIsNotAllowed(CurrentUser currentUser, bool trackingEnabled, DateOnly eventDate)
        {
            if (!AccessRules.CanRecordAttendance(currentUser))
            {
                return "The head coach has switched off attendance recording for your account.";
            }
            if (!trackingEnabled)
            {
                return "Attendance tracking is switched off for this team.";
            }
            if (eventDate > WeekDates.Today(_clock))
            {
                return "Attendance can be recorded from the day of the event.";
            }
            return null;
        }
    }
}
