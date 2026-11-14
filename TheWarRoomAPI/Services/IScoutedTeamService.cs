using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // The teams that appear on film: the team itself and the opponents it scouts
    public interface IScoutedTeamService
    {
        Task<List<ScoutedTeamDto>> GetTeamsAsync(CurrentUser currentUser);
        Task<ScoutedTeamDto?> GetTeamAsync(CurrentUser currentUser, int id);

        Task<ScoutedTeamDto> CreateAsync(CurrentUser currentUser, SaveScoutedTeamDto dto);
        Task<bool> UpdateAsync(CurrentUser currentUser, int id, SaveScoutedTeamDto dto);
        Task<bool> DeleteAsync(CurrentUser currentUser, int id);
    }
}
