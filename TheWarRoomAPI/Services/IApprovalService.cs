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

        // By the head coach
        Task<bool> ApproveAsync(CurrentUser currentUser, int id, ReviewDecisionDto dto);
        Task<bool> RejectAsync(CurrentUser currentUser, int id, ReviewDecisionDto dto);

        // By the coach who made the request, while it is still waiting
        Task<bool> ReviseAsync(CurrentUser currentUser, int id, ReviseApprovalRequestDto dto);
        Task<bool> WithdrawAsync(CurrentUser currentUser, int id);
    }
}
