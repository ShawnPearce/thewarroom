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

        // What the current user may do with this request
        public bool CanReview { get; set; }
        public bool CanRevise { get; set; }
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

        // True when the target has changed since a pending request was made; approving it would be refused
        public bool IsStale { get; set; }
    }
}
