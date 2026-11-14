using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    public class VideoService : IVideoService
    {
        private readonly AppDbContext _context;
        private readonly IClipService _clipService;

        public VideoService(AppDbContext context, IClipService clipService)
        {
            _context = context;
            _clipService = clipService;
        }

        public async Task<VideoDetailDto?> GetVideoAsync(CurrentUser currentUser, int id)
        {
            FilmRules.EnsureCoach(currentUser);

            var video = await _context.Videos
                .Where(v => v.Id == id && v.TeamId == currentUser.TeamId)
                .Select(v => new VideoDetailDto
                {
                    Id = v.Id,
                    GameId = v.GameId,
                    Title = v.Title,
                    Status = v.Status,
                    DurationMs = v.DurationMs,
                    HasFile = v.StorageKey != null,
                    ClipCount = v.Clips!.Count,
                    TaggedClipCount = v.Clips!.Count(c => c.Tag != null),
                    UploadedBy = v.UploadedBy!.FirstName + " " + v.UploadedBy.LastName,
                    CreatedAt = v.CreatedAt,
                    GameDate = v.Game!.GameDate,
                    GameLabel = v.Game.Label,
                    HomeTeamId = v.Game.HomeTeamId,
                    HomeTeamName = v.Game.HomeTeam!.Name,
                    AwayTeamId = v.Game.AwayTeamId,
                    AwayTeamName = v.Game.AwayTeam!.Name
                })
                .FirstOrDefaultAsync();
            if (video == null)
            {
                return null;
            }

            video.Clips = await _clipService.GetClipsAsync(currentUser, new ClipFilterDto { VideoId = video.Id });
            return video;
        }
    }
}
