using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // The uploaded film of a game
    public interface IVideoService
    {
        // A video with its game and its clips in the order they happen
        Task<VideoDetailDto?> GetVideoAsync(CurrentUser currentUser, int id);
    }
}
