using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Filters;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : WarRoomControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // POST: api/Auth/login
        [HttpPost("login")]
        [AllowWithoutSignIn]
        public async Task<ActionResult<LoginResultDto>> Login(LoginDto dto)
        {
            try
            {
                var result = await _authService.LoginAsync(dto);
                if (result == null)
                {
                    return Unauthorized("Invalid email or password.");   // 401 Unauthorized
                }

                return Ok(result);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (the account is deactivated)
            }
        }

        // POST: api/Auth/logout
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _authService.LogoutAsync(CurrentUser);
            return NoContent();   // 204 No Content
        }

        // POST: api/Auth/change-password
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            try
            {
                await _authService.ChangePasswordAsync(CurrentUser, dto);
                return NoContent();   // 204 No Content
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict with error message
            }
        }

        // GET: api/Auth/me
        [HttpGet("me")]
        public async Task<ActionResult<CurrentUserDto>> GetMe()
        {
            var me = await _authService.GetMeAsync(CurrentUser);
            if (me == null)
            {
                return NotFound();   // 404 Not Found
            }

            return Ok(me);   // 200 OK
        }
    }
}
