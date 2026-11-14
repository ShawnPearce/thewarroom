using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RosterController : WarRoomControllerBase
    {
        private readonly IRosterService _rosterService;

        public RosterController(IRosterService rosterService)
        {
            _rosterService = rosterService;
        }

        // GET: api/Roster
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RosterEntryDto>>> GetRoster()
        {
            var roster = await _rosterService.GetRosterAsync(CurrentUser);
            return Ok(roster);   // 200 OK
        }

        // GET: api/Roster/5   (5 is the player's user id)
        [HttpGet("{playerId}")]
        public async Task<ActionResult<RosterEntryDto>> GetPlayer(int playerId)
        {
            var player = await _rosterService.GetPlayerAsync(CurrentUser, playerId);
            if (player == null)
            {
                return NotFound();   // 404 Not Found
            }

            return Ok(player);   // 200 OK
        }

        // POST: api/Roster   (add a player)
        [HttpPost]
        public async Task<ActionResult<TemporaryPasswordDto>> CreatePlayer(CreatePlayerDto dto)
        {
            try
            {
                var created = await _rosterService.CreatePlayerAsync(CurrentUser, dto);
                return CreatedAtAction(nameof(GetPlayer), new { playerId = created.UserId }, created);   // 201 Created
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

        // PUT: api/Roster/5
        [HttpPut("{playerId}")]
        public async Task<IActionResult> UpdatePlayer(int playerId, UpdateRosterEntryDto dto)
        {
            try
            {
                var result = await _rosterService.UpdateAsync(CurrentUser, playerId, dto);
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
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict with error message
            }
        }

        // GET: api/Roster/5/coaches
        [HttpGet("{playerId}/coaches")]
        public async Task<ActionResult<IEnumerable<CoachSummaryDto>>> GetCoaches(int playerId)
        {
            try
            {
                var coaches = await _rosterService.GetCoachesAsync(CurrentUser, playerId);
                if (coaches == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(coaches);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }
    }
}
