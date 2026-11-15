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

        // ----- The video of a cutup -----

        // Asks for the cutup to be made into one video. Its plays are fixed from then on.
        Task<bool> RequestRenderAsync(CurrentUser currentUser, int id);

        // Turns the cutup back into a draft so its plays can be changed. A video that was made is thrown away.
        Task<bool> ReturnToDraftAsync(CurrentUser currentUser, int id);

        // How the browser gets the finished video to play it; null when there is nothing to play
        Task<FilmFile?> GetFileAsync(CurrentUser currentUser, int id);
    }
}
