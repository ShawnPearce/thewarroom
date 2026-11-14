using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // The players of a team and their football details
    public interface IRosterService
    {
        Task<List<RosterEntryDto>> GetRosterAsync(CurrentUser currentUser);
        Task<RosterEntryDto?> GetPlayerAsync(CurrentUser currentUser, int playerId);

        Task<TemporaryPasswordDto> CreatePlayerAsync(CurrentUser currentUser, CreatePlayerDto dto);
        Task<bool> UpdateAsync(CurrentUser currentUser, int playerId, UpdateRosterEntryDto dto);

        // The coaches of a player: the head coach, the coordinators of the player's units,
        // and the coaches of the player's groups. Returns null when the player is not on the team.
        Task<List<CoachSummaryDto>?> GetCoachesAsync(CurrentUser currentUser, int playerId);
    }
}
