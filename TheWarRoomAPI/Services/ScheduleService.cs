using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class ScheduleService : IScheduleService
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public ScheduleService(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        public async Task<List<ScheduledEventDto>> GetEventsAsync(CurrentUser currentUser, DateOnly? from, DateOnly? to, EventType? type)
        {
            var query = _context.ScheduledEvents.Where(e => e.TeamId == currentUser.TeamId);

            if (from != null)
            {
                query = query.Where(e => e.EventDate >= from);
            }
            if (to != null)
            {
                query = query.Where(e => e.EventDate <= to);
            }
            if (type != null)
            {
                query = query.Where(e => e.EventType == type);
            }

            bool canEdit = AccessRules.CanManageSchedule(currentUser);

            return await query
                .OrderBy(e => e.EventDate)
                .ThenBy(e => e.StartTime)
                .Select(e => new ScheduledEventDto
                {
                    Id = e.Id,
                    EventType = e.EventType,
                    Title = e.Title,
                    EventDate = e.EventDate,
                    StartTime = e.StartTime,
                    EndTime = e.EndTime,
                    Location = e.Location,
                    Notes = e.Notes,
                    AttendanceCount = e.AttendanceRecords!.Count,
                    CanEdit = canEdit
                })
                .ToListAsync();
        }

        public async Task<ScheduledEventDto?> GetEventAsync(CurrentUser currentUser, int id)
        {
            bool canEdit = AccessRules.CanManageSchedule(currentUser);

            return await _context.ScheduledEvents
                .Where(e => e.Id == id && e.TeamId == currentUser.TeamId)
                .Select(e => new ScheduledEventDto
                {
                    Id = e.Id,
                    EventType = e.EventType,
                    Title = e.Title,
                    EventDate = e.EventDate,
                    StartTime = e.StartTime,
                    EndTime = e.EndTime,
                    Location = e.Location,
                    Notes = e.Notes,
                    AttendanceCount = e.AttendanceRecords!.Count,
                    CanEdit = canEdit
                })
                .FirstOrDefaultAsync();
        }

        public async Task<ScheduledEventDto> CreateAsync(CurrentUser currentUser, SaveScheduledEventDto dto)
        {
            EnsureCanManage(currentUser);
            EnsureTimesMakeSense(dto);

            // Map DTO → entity (the server sets the team and who created it)
            var scheduledEvent = new ScheduledEvent
            {
                TeamId = currentUser.TeamId,
                EventType = dto.EventType,
                Title = dto.Title.Trim(),
                EventDate = dto.EventDate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Location = dto.Location.Trim(),
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                CreatedById = currentUser.Id,
                CreatedAt = _clock.GetUtcNow().UtcDateTime
            };

            _context.ScheduledEvents.Add(scheduledEvent);
            await _context.SaveChangesAsync();

            return (await GetEventAsync(currentUser, scheduledEvent.Id))!;
        }

        public async Task<bool> UpdateAsync(CurrentUser currentUser, int id, SaveScheduledEventDto dto)
        {
            var scheduledEvent = await _context.ScheduledEvents
                .FirstOrDefaultAsync(e => e.Id == id && e.TeamId == currentUser.TeamId);
            if (scheduledEvent == null)
            {
                return false;
            }

            EnsureCanManage(currentUser);
            EnsureTimesMakeSense(dto);

            scheduledEvent.EventType = dto.EventType;
            scheduledEvent.Title = dto.Title.Trim();
            scheduledEvent.EventDate = dto.EventDate;
            scheduledEvent.StartTime = dto.StartTime;
            scheduledEvent.EndTime = dto.EndTime;
            scheduledEvent.Location = dto.Location.Trim();
            scheduledEvent.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(CurrentUser currentUser, int id)
        {
            var scheduledEvent = await _context.ScheduledEvents
                .FirstOrDefaultAsync(e => e.Id == id && e.TeamId == currentUser.TeamId);
            if (scheduledEvent == null)
            {
                return false;
            }

            EnsureCanManage(currentUser);

            // Permission: deleting an event also deletes its attendance records,
            // so once attendance has been recorded only the head coach may delete it
            bool hasAttendance = await _context.AttendanceRecords.AnyAsync(a => a.ScheduledEventId == scheduledEvent.Id);
            if (hasAttendance && !currentUser.IsHeadCoach)
            {
                throw new ForbiddenException("Attendance has been recorded for this event, so only the head coach can delete it.");
            }

            _context.ScheduledEvents.Remove(scheduledEvent);   // its attendance records are deleted with it
            await _context.SaveChangesAsync();
            return true;
        }

        private static void EnsureCanManage(CurrentUser currentUser)
        {
            if (!AccessRules.CanManageSchedule(currentUser))
            {
                throw new ForbiddenException("Only the head coach and coordinators can change the schedule.");
            }
        }

        // Business rule: an event that has an end time must end after it starts
        private static void EnsureTimesMakeSense(SaveScheduledEventDto dto)
        {
            if (dto.EndTime != null && dto.EndTime <= dto.StartTime)
            {
                throw new InvalidOperationException("The end time must be after the start time.");
            }
        }
    }
}
