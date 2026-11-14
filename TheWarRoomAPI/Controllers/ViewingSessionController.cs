using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ViewingSessionController : WarRoomControllerBase
    {
        private readonly IStudyTimeService _studyTimeService;

        public ViewingSessionController(IStudyTimeService studyTimeService)
        {
            _studyTimeService = studyTimeService;
        }

        // POST: api/ViewingSession   (a film or playbook page reporting a player's active viewing time)
        [HttpPost]
        public async Task<ActionResult<StudyCreditDto>> Report(StudyReportDto dto)
        {
            try
            {
                var credit = await _studyTimeService.ReportAsync(CurrentUser, dto);
                if (credit == null)
                {
                    return NotFound();   // 404 Not Found (the playbook or cutup is not on this team)
                }

                return Ok(credit);   // 200 OK, with how much of the report was counted
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (a coach, or something not shared with this player)
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict with error message
            }
        }
    }
}
