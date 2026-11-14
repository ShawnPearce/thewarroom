using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public interface IAuthService
    {
        // Returns null when the email or password is wrong
        Task<LoginResultDto?> LoginAsync(LoginDto dto);

        // Opens a login session for a user whose identity has already been established
        Task<LoginResultDto> StartSessionAsync(User user);

        Task LogoutAsync(CurrentUser currentUser);

        Task ChangePasswordAsync(CurrentUser currentUser, ChangePasswordDto dto);

        Task<CurrentUserDto?> GetMeAsync(CurrentUser currentUser);

        // Works out who a request is acting as from the headers the MVC app sent.
        // Returns null when the user is unknown or deactivated, or the session token is not valid.
        Task<CurrentUser?> ResolveCurrentUserAsync(int userId, string? sessionToken);
    }
}
