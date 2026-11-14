using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScoutedTeamController : WarRoomControllerBase
    {
        private readonly IScoutedTeamService _scoutedTeamService;

        public ScoutedTeamController(IScoutedTeamService scoutedTeamService)
        {
            _scoutedTeamService = scoutedTeamService;
        }

        // GET: api/ScoutedTeam   (the teams on film: the team itself first, then its opponents)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ScoutedTeamDto>>> GetTeams()
        {
            try
            {
                var teams = await _scoutedTeamService.GetTeamsAsync(CurrentUser);
                return Ok(teams);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (players)
            }
        }

        // GET: api/ScoutedTeam/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ScoutedTeamDto>> GetTeam(int id)
        {
            try
            {
                var team = await _scoutedTeamService.GetTeamAsync(CurrentUser, id);
                if (team == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(team);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }

        // POST: api/ScoutedTeam
        [HttpPost]
        public async Task<ActionResult<ScoutedTeamDto>> CreateTeam(SaveScoutedTeamDto dto)
        {
            try
            {
                var created = await _scoutedTeamService.CreateAsync(CurrentUser, dto);
                return CreatedAtAction(nameof(GetTeam), new { id = created.Id }, created);   // 201 Created
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict with error message (the name is taken)
            }
        }

        // PUT: api/ScoutedTeam/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTeam(int id, SaveScoutedTeamDto dto)
        {
            try
            {
                var result = await _scoutedTeamService.UpdateAsync(CurrentUser, id, dto);
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

        // DELETE: api/ScoutedTeam/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTeam(int id)
        {
            try
            {
                var result = await _scoutedTeamService.DeleteAsync(CurrentUser, id);
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
                return Conflict(ex.Message);   // 409 Conflict (it has games on film, or it is the team itself)
            }
        }
    }
}
