using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlaybookController : WarRoomControllerBase
    {
        private readonly IPlaybookService _playbookService;

        public PlaybookController(IPlaybookService playbookService)
        {
            _playbookService = playbookService;
        }

        // GET: api/Playbook   (the playbooks the current user may read)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PlaybookDto>>> GetPlaybooks()
        {
            var playbooks = await _playbookService.GetPlaybooksAsync(CurrentUser);
            return Ok(playbooks);   // 200 OK
        }

        // GET: api/Playbook/5   (a playbook with its formations)
        [HttpGet("{id}")]
        public async Task<ActionResult<PlaybookDetailDto>> GetPlaybook(int id)
        {
            try
            {
                var playbook = await _playbookService.GetPlaybookAsync(CurrentUser, id);
                if (playbook == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(playbook);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (a player outside this unit)
            }
        }
    }
}
