using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScheduledEventController : WarRoomControllerBase
    {
        private readonly IScheduleService _scheduleService;

        public ScheduledEventController(IScheduleService scheduleService)
        {
            _scheduleService = scheduleService;
        }

        // GET: api/ScheduledEvent?from=2026-11-01&to=2026-11-30&type=Practice   (every filter is optional)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ScheduledEventDto>>> GetEvents(
            [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] EventType? type)
        {
            var events = await _scheduleService.GetEventsAsync(CurrentUser, from, to, type);
            return Ok(events);   // 200 OK
        }

        // GET: api/ScheduledEvent/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ScheduledEventDto>> GetEvent(int id)
        {
            var scheduledEvent = await _scheduleService.GetEventAsync(CurrentUser, id);
            if (scheduledEvent == null)
            {
                return NotFound();   // 404 Not Found
            }

            return Ok(scheduledEvent);   // 200 OK
        }

        // POST: api/ScheduledEvent
        [HttpPost]
        public async Task<ActionResult<ScheduledEventDto>> CreateEvent(SaveScheduledEventDto dto)
        {
            try
            {
                var created = await _scheduleService.CreateAsync(CurrentUser, dto);
                return CreatedAtAction(nameof(GetEvent), new { id = created.Id }, created);   // 201 Created
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

        // PUT: api/ScheduledEvent/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEvent(int id, SaveScheduledEventDto dto)
        {
            try
            {
                var result = await _scheduleService.UpdateAsync(CurrentUser, id, dto);
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

        // DELETE: api/ScheduledEvent/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            try
            {
                var result = await _scheduleService.DeleteAsync(CurrentUser, id);
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
    }
}
