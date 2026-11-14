using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FormationController : WarRoomControllerBase
    {
        private readonly IPlaybookService _playbookService;

        public FormationController(IPlaybookService playbookService)
        {
            _playbookService = playbookService;
        }

        // GET: api/Formation/5   (a formation with its plays; approved content only)
        [HttpGet("{id}")]
        public async Task<ActionResult<FormationDto>> GetFormation(int id)
        {
            try
            {
                var formation = await _playbookService.GetFormationAsync(CurrentUser, id);
                if (formation == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(formation);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }

        // POST: api/Formation
        [HttpPost]
        public async Task<ActionResult<PlaybookChangeResultDto>> CreateFormation(SaveFormationDto dto)
        {
            try
            {
                var result = await _playbookService.CreateFormationAsync(CurrentUser, dto);
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

        // PUT: api/Formation/5
        [HttpPut("{id}")]
        public async Task<ActionResult<PlaybookChangeResultDto>> UpdateFormation(int id, SaveFormationDto dto)
        {
            try
            {
                var result = await _playbookService.UpdateFormationAsync(CurrentUser, id, dto);
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

        // DELETE: api/Formation/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<PlaybookChangeResultDto>> DeleteFormation(int id)
        {
            try
            {
                var result = await _playbookService.DeleteFormationAsync(CurrentUser, id);
                return ChangeResult(result);
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict (the formation still has plays, for example)
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
