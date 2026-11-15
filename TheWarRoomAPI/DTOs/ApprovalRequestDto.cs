using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // One playbook change request, as a row in the approvals list
    public class ApprovalRequestDto
    {
        public int Id { get; set; }
        public int PlaybookId { get; set; }
        public string PlaybookName { get; set; } = string.Empty;
        public TeamUnit Unit { get; set; }
        public PlaybookTargetType TargetType { get; set; }
        public PlaybookChangeAction Action { get; set; }
        public int? FormationId { get; set; }
        public int? PlayId { get; set; }

        // The name of the formation or play when the request was made
        public string TargetName { get; set; } = string.Empty;
        public string? ProposedName { get; set; }

        public ApprovalStatus Status { get; set; }
        public bool AutoApproved { get; set; }

        public int RequestedById { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }

        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }

        // A change that was rejected can be revised and sent again. The new request points back at the rejected one,
        // and the rejected one points forward at the request that took its place.
        public int? ResubmittedFromId { get; set; }
        public int? ResubmittedAsId { get; set; }

        // True when the formation or play has changed, or is gone, since the request was made.
        // A waiting request can then no longer be approved, and a rejected one can no longer be sent again.
        public bool IsStale { get; set; }

        // What the current user may do with this request
        public bool CanReview { get; set; }
        public bool CanRevise { get; set; }

        // True for the coach who made a request that the head coach rejected, while it can still be revised and sent again
        public bool CanResubmit { get; set; }

        // True for that coach until they have opened the rejected request: the rejection is news to them
        public bool IsNewDecision { get; set; }
    }

    // A request with everything needed to compare before and after
    public class ApprovalRequestDetailDto : ApprovalRequestDto
    {
        // For a play: the formation it belongs to
        public string? FormationName { get; set; }

        // "Before": the approved content the change would replace (or did replace). Empty for a new formation.
        // For a new play it is the formation the play is drawn on.
        public string? BeforeName { get; set; }
        public string? BeforeJson { get; set; }
        public string? BeforeNotes { get; set; }

        // "After": what is proposed. Empty for a delete.
        public string? ProposedJson { get; set; }
        public string? ProposedNotes { get; set; }

        // For a request that was sent again: the head coach's decision on the request it was made from
        public string? EarlierReviewNote { get; set; }
        public string? EarlierReviewedBy { get; set; }
        public DateTime? EarlierReviewedAt { get; set; }

        // True when the current user may publish in this playbook, so that a change they hand in goes live at once
        public bool CanPublish { get; set; }
    }

    // What the menu tells a coach about playbook changes
    public class ApprovalNoticesDto
    {
        // For the head coach: the changes that are waiting for a decision
        public int WaitingForDecision { get; set; }

        // For the coach who made them: the changes the head coach rejected that the coach has not opened yet
        public int SentBack { get; set; }
    }
}
