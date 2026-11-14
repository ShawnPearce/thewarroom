using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // The team's schedule: practices, games and meetings
    public interface IScheduleService
    {
        // Events in date order. Each filter is optional.
        Task<List<ScheduledEventDto>> GetEventsAsync(CurrentUser currentUser, DateOnly? from, DateOnly? to, EventType? type);
        Task<ScheduledEventDto?> GetEventAsync(CurrentUser currentUser, int id);

        Task<ScheduledEventDto> CreateAsync(CurrentUser currentUser, SaveScheduledEventDto dto);
        Task<bool> UpdateAsync(CurrentUser currentUser, int id, SaveScheduledEventDto dto);
        Task<bool> DeleteAsync(CurrentUser currentUser, int id);
    }
}
