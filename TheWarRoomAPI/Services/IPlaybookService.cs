using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // Reading playbooks, and proposing changes to them.
    // Read methods return approved content only. Every change method returns what happened to the change:
    // published at once, or queued for the head coach. Null means the target is not on the current user's team.
    public interface IPlaybookService
    {
        Task<List<PlaybookDto>> GetPlaybooksAsync(CurrentUser currentUser);
        Task<PlaybookDetailDto?> GetPlaybookAsync(CurrentUser currentUser, int id);
        Task<FormationDto?> GetFormationAsync(CurrentUser currentUser, int id);
        Task<PlayDto?> GetPlayAsync(CurrentUser currentUser, int id);

        Task<PlaybookChangeResultDto?> CreateFormationAsync(CurrentUser currentUser, SaveFormationDto dto);
        Task<PlaybookChangeResultDto?> UpdateFormationAsync(CurrentUser currentUser, int id, SaveFormationDto dto);
        Task<PlaybookChangeResultDto?> DeleteFormationAsync(CurrentUser currentUser, int id);

        Task<PlaybookChangeResultDto?> CreatePlayAsync(CurrentUser currentUser, SavePlayDto dto);
        Task<PlaybookChangeResultDto?> UpdatePlayAsync(CurrentUser currentUser, int id, SavePlayDto dto);
        Task<PlaybookChangeResultDto?> DeletePlayAsync(CurrentUser currentUser, int id);
    }
}
