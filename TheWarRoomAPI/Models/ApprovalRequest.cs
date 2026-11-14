using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.Models
{
    // One proposed change to a playbook: creating, updating or deleting a formation or a play.
    // Every change goes through a request. It stays Pending until the head coach decides,
    // unless the person who made it may publish directly, in which case it is approved at once.
    public class ApprovalRequest
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Foreign key for the Playbook entity
        public int PlaybookId { get; set; }
        public Playbook? Playbook { get; set; }

        public PlaybookTargetType TargetType { get; set; }
        public PlaybookChangeAction Action { get; set; }

        // The formation the request is about. For a play, the formation it belongs to.
        // Emptied if that formation is deleted later, so the history is kept.
        public int? FormationId { get; set; }
        public Formation? Formation { get; set; }

        // The play the request is about (empty for formation requests and for new plays)
        public int? PlayId { get; set; }
        public Play? Play { get; set; }

        // The version the target had when the request was made (empty for Create)
        public int? BaseVersion { get; set; }

        // The name the target had when the request was made, kept so the history still reads well after a delete
        [Required]
        [StringLength(60)]
        public string TargetName { get; set; } = string.Empty;

        // What is proposed (empty for Delete)
        [StringLength(60)]
        public string? ProposedName { get; set; }
        public string? ProposedJson { get; set; }

        [StringLength(1000)]
        public string? ProposedNotes { get; set; }

        // What the target looked like just before an approved change replaced it (filled in when the change is applied),
        // so the history can still show before and after
        [StringLength(60)]
        public string? PreviousName { get; set; }
        public string? PreviousJson { get; set; }

        [StringLength(1000)]
        public string? PreviousNotes { get; set; }

        public ApprovalStatus Status { get; set; }

        // True when the change was published straight away because the requester had the right to
        public bool AutoApproved { get; set; }

        // Foreign key for the User entity (who asked for the change)
        public int RequestedById { get; set; }
        public User? RequestedBy { get; set; }
        public DateTime RequestedAt { get; set; }

        // Foreign key for the User entity (who decided), with the decision
        public int? ReviewedById { get; set; }
        public User? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }

        [StringLength(500)]
        public string? ReviewNote { get; set; }
    }
}
