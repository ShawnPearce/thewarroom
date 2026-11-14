using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlayController : WarRoomControllerBase
    {
        private readonly IPlaybookService _playbookService;

        public PlayController(IPlaybookService playbookService)
        {
            _playbookService = playbookService;
        }

        // GET: api/Play/5   (always the last approved version)
        [HttpGet("{id}")]
        public async Task<ActionResult<PlayDto>> GetPlay(int id)
        {
            try
            {
                var play = await _playbookService.GetPlayAsync(CurrentUser, id);
                if (play == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(play);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }

        // POST: api/Play
        [HttpPost]
        public async Task<ActionResult<PlaybookChangeResultDto>> CreatePlay(SavePlayDto dto)
        {
            try
            {
                var result = await _playbookService.CreatePlayAsync(CurrentUser, dto);
                return ChangeResult(result);
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (outside the coach's playbooks)
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict with error message
            }
        }

        // PUT: api/Play/5
        [HttpPut("{id}")]
        public async Task<ActionResult<PlaybookChangeResultDto>> UpdatePlay(int id, SavePlayDto dto)
        {
            try
            {
                var result = await _playbookService.UpdatePlayAsync(CurrentUser, id, dto);
                return ChangeResult(result);
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

        // DELETE: api/Play/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<PlaybookChangeResultDto>> DeletePlay(int id)
        {
            try
            {
                var result = await _playbookService.DeletePlayAsync(CurrentUser, id);
                return ChangeResult(result);
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

        // A playbook change either went live or is waiting for approval; the status code says which
        private ActionResult<PlaybookChangeResultDto> ChangeResult(PlaybookChangeResultDto? result)
        {
            if (result == null)
            {
                return NotFound();   // 404 Not Found
            }

            if (result.Published)
            {
                return Ok(result);   // 200 OK: the change is live
            }

            return Accepted(result);   // 202 Accepted: waiting for the head coach's approval
        }
    }
}
