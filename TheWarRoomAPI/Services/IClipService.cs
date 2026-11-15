using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // The plays cut out of a video, and the tag a coach puts on each
    public interface IClipService
    {
        // In the order the plays happen: by game date, then by where they are in their video
        Task<List<ClipDto>> GetClipsAsync(CurrentUser currentUser, ClipFilterDto filter);
        Task<ClipDto?> GetClipAsync(CurrentUser currentUser, int id);

        // Null means the video is not on the current user's team
        Task<ClipDto?> CreateAsync(CurrentUser currentUser, SaveClipDto dto);
        Task<bool> UpdateAsync(CurrentUser currentUser, int id, SaveClipDto dto);
        Task<bool> DeleteAsync(CurrentUser currentUser, int id);

        // The arrows and text drawn on a play, and the moments the video stops to show them
        Task<bool> SaveAnnotationsAsync(CurrentUser currentUser, int id, SaveAnnotationsDto dto);
    }
}
