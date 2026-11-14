using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DepthChartController : WarRoomControllerBase
    {
        private readonly IDepthChartService _depthChartService;

        public DepthChartController(IDepthChartService depthChartService)
        {
            _depthChartService = depthChartService;
        }

        // GET: api/DepthChart/weeks
        [HttpGet("weeks")]
        public async Task<ActionResult<IEnumerable<DepthChartWeekDto>>> GetWeeks()
        {
            var weeks = await _depthChartService.GetWeeksAsync(CurrentUser);
            return Ok(weeks);   // 200 OK
        }

        // POST: api/DepthChart/weeks
        [HttpPost("weeks")]
        public async Task<ActionResult<DepthChartWeekDto>> CreateWeek(CreateDepthChartWeekDto dto)
        {
            try
            {
                var created = await _depthChartService.CreateWeekAsync(CurrentUser, dto);
                return CreatedAtAction(nameof(GetWeek), new { weekId = created.Id }, created);   // 201 Created
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict with error message
            }
        }

        // DELETE: api/DepthChart/weeks/5
        [HttpDelete("weeks/{weekId}")]
        public async Task<IActionResult> DeleteWeek(int weekId)
        {
            try
            {
                var result = await _depthChartService.DeleteWeekAsync(CurrentUser, weekId);
                if (!result)
                {
                    return NotFound();   // 404 Not Found
                }

                return NoContent();   // 204 No Content
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }

        // GET: api/DepthChart/weeks/5   (the whole chart of one week)
        [HttpGet("weeks/{weekId}")]
        public async Task<ActionResult<DepthChartDto>> GetWeek(int weekId)
        {
            var chart = await _depthChartService.GetWeekAsync(CurrentUser, weekId);
            if (chart == null)
            {
                return NotFound();   // 404 Not Found
            }

            return Ok(chart);   // 200 OK
        }

        // GET: api/DepthChart/current   (this week's chart, or the nearest one)
        [HttpGet("current")]
        public async Task<ActionResult<DepthChartDto>> GetCurrentWeek()
        {
            var chart = await _depthChartService.GetCurrentWeekAsync(CurrentUser);
            if (chart == null)
            {
                return NotFound();   // 404 Not Found: the team has no depth chart yet
            }

            return Ok(chart);   // 200 OK
        }

        // GET: api/DepthChart/weeks/5/positions/12   (one position, for the editing page)
        [HttpGet("weeks/{weekId}/positions/{positionId}")]
        public async Task<ActionResult<DepthChartSlotDto>> GetSlot(int weekId, int positionId)
        {
            var slot = await _depthChartService.GetSlotAsync(CurrentUser, weekId, positionId);
            if (slot == null)
            {
                return NotFound();   // 404 Not Found
            }

            return Ok(slot);   // 200 OK
        }

        // POST: api/DepthChart/weeks/5/positions/12/players   (add a player at the bottom)
        [HttpPost("weeks/{weekId}/positions/{positionId}/players")]
        public async Task<ActionResult<DepthChartSlotDto>> AddPlayer(int weekId, int positionId, AddPlacementDto dto)
        {
            try
            {
                var slot = await _depthChartService.AddPlayerAsync(CurrentUser, weekId, positionId, dto);
                if (slot == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(slot);   // 200 OK with the updated position
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict with error message
            }
        }

        // POST: api/DepthChart/placements/40/move   (one step up or down)
        [HttpPost("placements/{placementId}/move")]
        public async Task<IActionResult> MovePlacement(int placementId, MovePlacementDto dto)
        {
            try
            {
                var result = await _depthChartService.MovePlacementAsync(CurrentUser, placementId, dto.Direction);
                if (!result)
                {
                    return NotFound();   // 404 Not Found
                }

                return NoContent();   // 204 No Content
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }

        // DELETE: api/DepthChart/placements/40
        [HttpDelete("placements/{placementId}")]
        public async Task<IActionResult> RemovePlacement(int placementId)
        {
            try
            {
                var result = await _depthChartService.RemovePlacementAsync(CurrentUser, placementId);
                if (!result)
                {
                    return NotFound();   // 404 Not Found
                }

                return NoContent();   // 204 No Content
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }

        // GET: api/DepthChart/weeks/5/my   (the signed-in player's own placements)
        [HttpGet("weeks/{weekId}/my")]
        public async Task<ActionResult<IEnumerable<MyPlacementDto>>> GetMyPlacements(int weekId)
        {
            var placements = await _depthChartService.GetMyPlacementsAsync(CurrentUser, weekId);
            if (placements == null)
            {
                return NotFound();   // 404 Not Found
            }

            return Ok(placements);   // 200 OK
        }
    }
}
