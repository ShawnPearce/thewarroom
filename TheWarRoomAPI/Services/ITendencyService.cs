using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // Run and pass tendencies, counted from the plays coaches have tagged
    public interface ITendencyService
    {
        Task<TendencyReportDto> GetReportAsync(CurrentUser currentUser, TendencyFilterDto filter);
    }
}
