using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // Weekly depth charts. A change is live as soon as it is saved; there is no approval step.
    public interface IDepthChartService
    {
        Task<List<DepthChartWeekDto>> GetWeeksAsync(CurrentUser currentUser);
        Task<DepthChartWeekDto> CreateWeekAsync(CurrentUser currentUser, CreateDepthChartWeekDto dto);
        Task<bool> DeleteWeekAsync(CurrentUser currentUser, int weekId);

        // The whole chart of one week. Returns null when the week is not on the current user's team.
        Task<DepthChartDto?> GetWeekAsync(CurrentUser currentUser, int weekId);

        // The chart for this week, or the nearest one when this week has none. Null when the team has no weeks yet.
        Task<DepthChartDto?> GetCurrentWeekAsync(CurrentUser currentUser);

        // One position in one week, for the editing page
        Task<DepthChartSlotDto?> GetSlotAsync(CurrentUser currentUser, int weekId, int positionId);

        Task<DepthChartSlotDto?> AddPlayerAsync(CurrentUser currentUser, int weekId, int positionId, AddPlacementDto dto);
        Task<bool> MovePlacementAsync(CurrentUser currentUser, int placementId, MoveDirection direction);
        Task<bool> RemovePlacementAsync(CurrentUser currentUser, int placementId);

        // Where the current user stands in one week. Returns null when the week is not on their team.
        Task<List<MyPlacementDto>?> GetMyPlacementsAsync(CurrentUser currentUser, int weekId);
    }
}
