using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AttendanceController : WarRoomControllerBase
    {
        private readonly IAttendanceService _attendanceService;

        public AttendanceController(IAttendanceService attendanceService)
        {
            _attendanceService = attendanceService;
        }

        // GET: api/Attendance/event/5   (the attendance sheet of one event, for coaches)
        [HttpGet("event/{eventId}")]
        public async Task<ActionResult<AttendanceSheetDto>> GetSheet(int eventId)
        {
            try
            {
                var sheet = await _attendanceService.GetSheetAsync(CurrentUser, eventId);
                if (sheet == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(sheet);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }

        // PUT: api/Attendance/event/5   (save the sheet)
        [HttpPut("event/{eventId}")]
        public async Task<IActionResult> Record(int eventId, RecordAttendanceDto dto)
        {
            try
            {
                var result = await _attendanceService.RecordAsync(CurrentUser, eventId, dto);
                if (!result)
                {
                    return NotFound();   // 404 Not Found
                }

                return NoContent();   // 204 No Content
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (not a permitted coach)
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict (tracking is switched off, or the event has not happened yet)
            }
        }

        // GET: api/Attendance/my   (the signed-in player's own records)
        [HttpGet("my")]
        public async Task<ActionResult<IEnumerable<MyAttendanceDto>>> GetMyAttendance()
        {
            var records = await _attendanceService.GetMyAttendanceAsync(CurrentUser);
            return Ok(records);   // 200 OK
        }

        // GET: api/Attendance/summary?from=2026-09-01&to=2026-11-30   (counts per player, for coaches)
        [HttpGet("summary")]
        public async Task<ActionResult<IEnumerable<AttendanceSummaryDto>>> GetSummary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        {
            try
            {
                var summary = await _attendanceService.GetSummaryAsync(CurrentUser, from, to);
                return Ok(summary);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }
    }
}
