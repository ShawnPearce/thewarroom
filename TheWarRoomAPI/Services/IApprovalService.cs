using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // The playbook change requests: the head coach's queue, and a coach's own requests
    public interface IApprovalService
    {
        // The requests the current user may see, newest first. The status filter is optional.
        Task<List<ApprovalRequestDto>> GetRequestsAsync(CurrentUser currentUser, ApprovalStatus? status);
        Task<ApprovalRequestDetailDto?> GetRequestAsync(CurrentUser currentUser, int id);

        // What the current user is told about: changes waiting for the head coach, changes sent back to a coach
        Task<ApprovalNoticesDto> GetNoticesAsync(CurrentUser currentUser);

        // By the head coach
        Task<bool> ApproveAsync(CurrentUser currentUser, int id, ReviewDecisionDto dto);
        Task<bool> RejectAsync(CurrentUser currentUser, int id, ReviewDecisionDto dto);

        // By the coach who made the request, while it is still waiting
        Task<bool> ReviseAsync(CurrentUser currentUser, int id, ReviseApprovalRequestDto dto);
        Task<bool> WithdrawAsync(CurrentUser currentUser, int id);

        // By the coach who made the request, after the head coach rejected it:
        // the rejection is marked as read, and the change can be revised and sent again as a new request
        Task<bool> MarkSeenAsync(CurrentUser currentUser, int id);
        Task<PlaybookChangeResultDto?> ResubmitAsync(CurrentUser currentUser, int id, ReviseApprovalRequestDto dto);
    }
}
