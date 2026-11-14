using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // The activity report: login time, film time and playbook time, kept apart
    public interface IActivityService
    {
        // A row for every player the current coach may see, for the week that contains the given day
        // (this week when no day is given)
        Task<ActivityReportDto> GetWeekAsync(CurrentUser currentUser, DateOnly? weekOf);

        // One player's last weeks, newest first. Null when the player is not on the current user's team.
        Task<PlayerActivityDto?> GetPlayerAsync(CurrentUser currentUser, int playerId, int weeks);
    }
}
