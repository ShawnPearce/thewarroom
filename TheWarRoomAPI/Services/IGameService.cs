using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // The games the team has film of
    public interface IGameService
    {
        // Newest first. With scoutedTeamId, only the games that team played in.
        Task<List<GameDto>> GetGamesAsync(CurrentUser currentUser, int? scoutedTeamId);
        Task<GameDetailDto?> GetGameAsync(CurrentUser currentUser, int id);

        Task<GameDto> CreateAsync(CurrentUser currentUser, SaveGameDto dto);
        Task<bool> UpdateAsync(CurrentUser currentUser, int id, SaveGameDto dto);
        Task<bool> DeleteAsync(CurrentUser currentUser, int id);
    }
}
