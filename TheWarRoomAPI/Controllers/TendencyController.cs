using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TendencyController : WarRoomControllerBase
    {
        private readonly ITendencyService _tendencyService;

        public TendencyController(ITendencyService tendencyService)
        {
            _tendencyService = tendencyService;
        }

        // GET: api/Tendency?offenceTeamId=2&gameIds=2&gameIds=3&down=1&distance=Short   (every filter is optional)
        [HttpGet]
        public async Task<ActionResult<TendencyReportDto>> GetReport([FromQuery] TendencyFilterDto filter)
        {
            try
            {
                var report = await _tendencyService.GetReportAsync(CurrentUser, filter);
                return Ok(report);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (players)
            }
        }
    }
}
