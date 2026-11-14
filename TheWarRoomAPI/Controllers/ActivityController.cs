using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ActivityController : WarRoomControllerBase
    {
        private readonly IActivityService _activityService;

        public ActivityController(IActivityService activityService)
        {
            _activityService = activityService;
        }

        // GET: api/Activity?weekOf=2026-11-11   (the team's players for that week; this week when no day is given)
        [HttpGet]
        public async Task<ActionResult<ActivityReportDto>> GetWeek([FromQuery] DateOnly? weekOf)
        {
            try
            {
                var report = await _activityService.GetWeekAsync(CurrentUser, weekOf);
                return Ok(report);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (players)
            }
        }

        // GET: api/Activity/player/14?weeks=8   (one player, week by week)
        [HttpGet("player/{playerId}")]
        public async Task<ActionResult<PlayerActivityDto>> GetPlayer(int playerId, [FromQuery] int weeks = 8)
        {
            try
            {
                var activity = await _activityService.GetPlayerAsync(CurrentUser, playerId, weeks);
                if (activity == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(activity);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (outside the coach's groups, or another player)
            }
        }

        // GET: api/Activity/me?weeks=8   (the signed-in player's own activity)
        [HttpGet("me")]
        public async Task<ActionResult<PlayerActivityDto>> GetMine([FromQuery] int weeks = 8)
        {
            var activity = await _activityService.GetPlayerAsync(CurrentUser, CurrentUser.Id, weeks);
            if (activity == null)
            {
                return NotFound();   // 404 Not Found (coaches have no study time of their own)
            }

            return Ok(activity);   // 200 OK
        }
    }
}
