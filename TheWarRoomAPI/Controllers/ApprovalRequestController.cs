using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApprovalRequestController : WarRoomControllerBase
    {
        private readonly IApprovalService _approvalService;

        public ApprovalRequestController(IApprovalService approvalService)
        {
            _approvalService = approvalService;
        }

        // GET: api/ApprovalRequest?status=Pending   (the status filter is optional)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ApprovalRequestDto>>> GetRequests([FromQuery] ApprovalStatus? status)
        {
            try
            {
                var requests = await _approvalService.GetRequestsAsync(CurrentUser, status);
                return Ok(requests);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (players)
            }
        }

        // GET: api/ApprovalRequest/notices   (what the menu counts: waiting for the head coach, sent back to a coach)
        [HttpGet("notices")]
        public async Task<ActionResult<ApprovalNoticesDto>> GetNotices()
        {
            try
            {
                var notices = await _approvalService.GetNoticesAsync(CurrentUser);
                return Ok(notices);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (players)
            }
        }

        // GET: api/ApprovalRequest/5   (with the before and after diagrams)
        [HttpGet("{id}")]
        public async Task<ActionResult<ApprovalRequestDetailDto>> GetRequest(int id)
        {
            try
            {
                var request = await _approvalService.GetRequestAsync(CurrentUser, id);
                if (request == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(request);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }

        // POST: api/ApprovalRequest/5/approve   (head coach)
        [HttpPost("{id}/approve")]
        public async Task<IActionResult> Approve(int id, ReviewDecisionDto dto)
        {
            try
            {
                var result = await _approvalService.ApproveAsync(CurrentUser, id, dto);
                if (!result)
                {
                    return NotFound();   // 404 Not Found
                }

                return NoContent();   // 204 No Content: the change is now live
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict (already decided, or the target changed in the meantime)
            }
        }

        // POST: api/ApprovalRequest/5/reject   (head coach)
        [HttpPost("{id}/reject")]
        public async Task<IActionResult> Reject(int id, ReviewDecisionDto dto)
        {
            try
            {
                var result = await _approvalService.RejectAsync(CurrentUser, id, dto);
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
                return Conflict(ex.Message);   // 409 Conflict (already decided)
            }
        }

        // PUT: api/ApprovalRequest/5   (the requester changes what they proposed)
        [HttpPut("{id}")]
        public async Task<IActionResult> Revise(int id, ReviseApprovalRequestDto dto)
        {
            try
            {
                var result = await _approvalService.ReviseAsync(CurrentUser, id, dto);
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

        // POST: api/ApprovalRequest/5/withdraw   (the requester takes it back)
        [HttpPost("{id}/withdraw")]
        public async Task<IActionResult> Withdraw(int id)
        {
            try
            {
                var result = await _approvalService.WithdrawAsync(CurrentUser, id);
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
                return Conflict(ex.Message);   // 409 Conflict (already decided)
            }
        }

        // POST: api/ApprovalRequest/5/seen   (the requester has read the head coach's rejection)
        [HttpPost("{id}/seen")]
        public async Task<IActionResult> MarkSeen(int id)
        {
            try
            {
                var result = await _approvalService.MarkSeenAsync(CurrentUser, id);
                if (!result)
                {
                    return NotFound();   // 404 Not Found
                }

                return NoContent();   // 204 No Content
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (somebody else's request)
            }
        }

        // POST: api/ApprovalRequest/5/resubmit   (the requester revises a rejected change and sends it again)
        [HttpPost("{id}/resubmit")]
        public async Task<ActionResult<PlaybookChangeResultDto>> Resubmit(int id, ReviseApprovalRequestDto dto)
        {
            try
            {
                var result = await _approvalService.ResubmitAsync(CurrentUser, id, dto);
                if (result == null)
                {
                    return NotFound();   // 404 Not Found
                }

                if (result.Published)
                {
                    return Ok(result);   // 200 OK: this coach may publish by now, so the change is live
                }

                return Accepted(result);   // 202 Accepted: a new request is waiting for the head coach
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict (not rejected, sent again already, or the playbook changed)
            }
        }
    }
}
