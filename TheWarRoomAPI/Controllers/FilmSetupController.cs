using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilmSetupController : WarRoomControllerBase
    {
        private readonly IVideoService _videoService;

        public FilmSetupController(IVideoService videoService)
        {
            _videoService = videoService;
        }

        // GET: api/FilmSetup   (whether this server can take video, and where it keeps it)
        [HttpGet]
        public ActionResult<FilmSetupDto> GetSetup()
        {
            try
            {
                return Ok(_videoService.GetSetup(CurrentUser));   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (players)
            }
        }
    }
}
