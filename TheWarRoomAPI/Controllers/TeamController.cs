using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Filters;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TeamController : WarRoomControllerBase
    {
        private readonly ITeamService _teamService;

        public TeamController(ITeamService teamService)
        {
            _teamService = teamService;
        }

        // GET: api/Team
        [HttpGet]
        public async Task<ActionResult<TeamDto>> GetTeam()
        {
            var team = await _teamService.GetAsync(CurrentUser);
            if (team == null)
            {
                return NotFound();   // 404 Not Found
            }

            return Ok(team);   // 200 OK
        }

        // PUT: api/Team
        [HttpPut]
        public async Task<IActionResult> UpdateTeam(UpdateTeamDto dto)
        {
            try
            {
                var result = await _teamService.UpdateAsync(CurrentUser, dto);
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
                return Conflict(ex.Message);   // 409 Conflict (an opponent on film already has the new name)
            }
        }

        // POST: api/Team/register
        [HttpPost("register")]
        [AllowWithoutSignIn]
        public async Task<ActionResult<LoginResultDto>> Register(RegisterTeamDto dto)
        {
            try
            {
                var result = await _teamService.RegisterAsync(dto);
                return CreatedAtAction(nameof(GetTeam), null, result);   // 201 Created
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict with error message (email already used)
            }
        }
    }
}
