using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // Accounts: the coaching staff, and the account details of any user
    public interface IUserService
    {
        Task<List<UserDto>> GetStaffAsync(CurrentUser currentUser);
        Task<UserDto?> GetByIdAsync(CurrentUser currentUser, int id);

        Task<TemporaryPasswordDto> CreateCoachAsync(CurrentUser currentUser, CreateCoachDto dto);

        Task<bool> UpdateAsync(CurrentUser currentUser, int id, UpdateUserDto dto);
        Task<bool> UpdatePermissionsAsync(CurrentUser currentUser, int id, UpdatePermissionsDto dto);
        Task<bool> SetActiveAsync(CurrentUser currentUser, int id, bool isActive);

        // Returns null when the user is not on the current user's team
        Task<TemporaryPasswordDto?> ResetPasswordAsync(CurrentUser currentUser, int id);
    }
}
