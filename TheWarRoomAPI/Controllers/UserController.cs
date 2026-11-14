using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : WarRoomControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        // GET: api/User   (the coaching staff)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetStaff()
        {
            var staff = await _userService.GetStaffAsync(CurrentUser);
            return Ok(staff);   // 200 OK
        }

        // GET: api/User/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            var user = await _userService.GetByIdAsync(CurrentUser, id);
            if (user == null)
            {
                return NotFound();   // 404 Not Found
            }

            return Ok(user);   // 200 OK
        }

        // POST: api/User   (add a coach)
        [HttpPost]
        public async Task<ActionResult<TemporaryPasswordDto>> CreateCoach(CreateCoachDto dto)
        {
            try
            {
                var created = await _userService.CreateCoachAsync(CurrentUser, dto);
                return CreatedAtAction(nameof(GetUser), new { id = created.UserId }, created);   // 201 Created
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

        // PUT: api/User/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, UpdateUserDto dto)
        {
            try
            {
                var result = await _userService.UpdateAsync(CurrentUser, id, dto);
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

        // PUT: api/User/5/permissions
        [HttpPut("{id}/permissions")]
        public async Task<IActionResult> UpdatePermissions(int id, UpdatePermissionsDto dto)
        {
            try
            {
                var result = await _userService.UpdatePermissionsAsync(CurrentUser, id, dto);
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

        // PUT: api/User/5/active
        [HttpPut("{id}/active")]
        public async Task<IActionResult> SetActive(int id, SetActiveDto dto)
        {
            try
            {
                var result = await _userService.SetActiveAsync(CurrentUser, id, dto.IsActive);
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

        // POST: api/User/5/reset-password
        [HttpPost("{id}/reset-password")]
        public async Task<ActionResult<TemporaryPasswordDto>> ResetPassword(int id)
        {
            try
            {
                var result = await _userService.ResetPasswordAsync(CurrentUser, id);
                if (result == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(result);   // 200 OK with the temporary password, shown once
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
