using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VideoController : WarRoomControllerBase
    {
        private readonly IVideoService _videoService;

        public VideoController(IVideoService videoService)
        {
            _videoService = videoService;
        }

        // GET: api/Video/5   (a video with its game and its clips in the order they happen)
        [HttpGet("{id}")]
        public async Task<ActionResult<VideoDetailDto>> GetVideo(int id)
        {
            try
            {
                var video = await _videoService.GetVideoAsync(CurrentUser, id);
                if (video == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(video);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (players)
            }
        }
    }
}
