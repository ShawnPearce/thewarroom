using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    public interface ITeamService
    {
        // Creates the team and its head coach, then signs the head coach in
        Task<LoginResultDto> RegisterAsync(RegisterTeamDto dto);

        Task<TeamDto?> GetAsync(CurrentUser currentUser);

        Task<bool> UpdateAsync(CurrentUser currentUser, UpdateTeamDto dto);
    }
}
