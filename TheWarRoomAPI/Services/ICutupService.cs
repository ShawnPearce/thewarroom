using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // Cutups: edited film made of chosen plays in a chosen order, and who it is shared with.
    // Coaches build and share them. A player gets only the cutups shared with the whole team,
    // with one of the player's groups, or with the player directly.
    public interface ICutupService
    {
        Task<List<CutupDto>> GetCutupsAsync(CurrentUser currentUser);
        Task<CutupDetailDto?> GetCutupAsync(CurrentUser currentUser, int id);

        Task<CutupDto> CreateAsync(CurrentUser currentUser, SaveCutupDto dto);
        Task<bool> UpdateAsync(CurrentUser currentUser, int id, SaveCutupDto dto);
        Task<bool> DeleteAsync(CurrentUser currentUser, int id);

        // The plays of a cutup, which can be changed while it is a draft
        Task<bool> AddClipsAsync(CurrentUser currentUser, int id, AddCutupClipsDto dto);
        Task<bool> MovePlayAsync(CurrentUser currentUser, int playId, MoveDirection direction);
        Task<bool> RemovePlayAsync(CurrentUser currentUser, int playId);

        Task<bool> SetSharingAsync(CurrentUser currentUser, int id, SetCutupSharingDto dto);
    }
}
