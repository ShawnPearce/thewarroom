using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PositionController : WarRoomControllerBase
    {
        private readonly IPositionGroupService _positionGroupService;

        public PositionController(IPositionGroupService positionGroupService)
        {
            _positionGroupService = positionGroupService;
        }

        // GET: api/Position/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PositionDto>> GetPosition(int id)
        {
            var position = await _positionGroupService.GetPositionAsync(CurrentUser, id);
            if (position == null)
            {
                return NotFound();   // 404 Not Found
            }

            return Ok(position);   // 200 OK
        }

        // POST: api/Position
        [HttpPost]
        public async Task<ActionResult<PositionDto>> CreatePosition(SavePositionDto dto)
        {
            try
            {
                var created = await _positionGroupService.CreatePositionAsync(CurrentUser, dto);
                return CreatedAtAction(nameof(GetPosition), new { id = created.Id }, created);   // 201 Created
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

        // PUT: api/Position/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePosition(int id, SavePositionDto dto)
        {
            try
            {
                var result = await _positionGroupService.UpdatePositionAsync(CurrentUser, id, dto);
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

        // DELETE: api/Position/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePosition(int id)
        {
            try
            {
                var result = await _positionGroupService.DeletePositionAsync(CurrentUser, id);
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
    }
}
