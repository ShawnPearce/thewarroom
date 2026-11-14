using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // Position groups, the positions inside them, and which coaches and players belong to each group
    public interface IPositionGroupService
    {
        Task<List<PositionGroupDto>> GetGroupsAsync(CurrentUser currentUser);
        Task<PositionGroupDto?> GetGroupAsync(CurrentUser currentUser, int id);

        Task<PositionGroupDto> CreateGroupAsync(CurrentUser currentUser, SavePositionGroupDto dto);
        Task<bool> RenameGroupAsync(CurrentUser currentUser, int id, SavePositionGroupDto dto);
        Task<bool> DeleteGroupAsync(CurrentUser currentUser, int id);

        Task<bool> SetCoachesAsync(CurrentUser currentUser, int id, SetGroupMembersDto dto);
        Task<bool> SetPlayersAsync(CurrentUser currentUser, int id, SetGroupMembersDto dto);

        Task<PositionDto?> GetPositionAsync(CurrentUser currentUser, int id);
        Task<PositionDto> CreatePositionAsync(CurrentUser currentUser, SavePositionDto dto);
        Task<bool> UpdatePositionAsync(CurrentUser currentUser, int id, SavePositionDto dto);
        Task<bool> DeletePositionAsync(CurrentUser currentUser, int id);
    }
}
