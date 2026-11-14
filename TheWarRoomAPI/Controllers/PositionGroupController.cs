using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PositionGroupController : WarRoomControllerBase
    {
        private readonly IPositionGroupService _positionGroupService;

        public PositionGroupController(IPositionGroupService positionGroupService)
        {
            _positionGroupService = positionGroupService;
        }

        // GET: api/PositionGroup
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PositionGroupDto>>> GetGroups()
        {
            var groups = await _positionGroupService.GetGroupsAsync(CurrentUser);
            return Ok(groups);   // 200 OK
        }

        // GET: api/PositionGroup/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PositionGroupDto>> GetGroup(int id)
        {
            var group = await _positionGroupService.GetGroupAsync(CurrentUser, id);
            if (group == null)
            {
                return NotFound();   // 404 Not Found
            }

            return Ok(group);   // 200 OK
        }

        // POST: api/PositionGroup
        [HttpPost]
        public async Task<ActionResult<PositionGroupDto>> CreateGroup(SavePositionGroupDto dto)
        {
            try
            {
                var created = await _positionGroupService.CreateGroupAsync(CurrentUser, dto);
                return CreatedAtAction(nameof(GetGroup), new { id = created.Id }, created);   // 201 Created
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

        // PUT: api/PositionGroup/5   (rename)
        [HttpPut("{id}")]
        public async Task<IActionResult> RenameGroup(int id, SavePositionGroupDto dto)
        {
            try
            {
                var result = await _positionGroupService.RenameGroupAsync(CurrentUser, id, dto);
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

        // DELETE: api/PositionGroup/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGroup(int id)
        {
            try
            {
                var result = await _positionGroupService.DeleteGroupAsync(CurrentUser, id);
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

        // PUT: api/PositionGroup/5/coaches   (the full list of the group's coaches)
        [HttpPut("{id}/coaches")]
        public async Task<IActionResult> SetCoaches(int id, SetGroupMembersDto dto)
        {
            try
            {
                var result = await _positionGroupService.SetCoachesAsync(CurrentUser, id, dto);
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

        // PUT: api/PositionGroup/5/players   (the full list of the group's players)
        [HttpPut("{id}/players")]
        public async Task<IActionResult> SetPlayers(int id, SetGroupMembersDto dto)
        {
            try
            {
                var result = await _positionGroupService.SetPlayersAsync(CurrentUser, id, dto);
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
