using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // A formation with its plays. Only approved content is ever returned here.
    public class FormationDto
    {
        public int Id { get; set; }
        public int PlaybookId { get; set; }
        public string PlaybookName { get; set; } = string.Empty;
        public TeamUnit Unit { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DiagramJson { get; set; } = string.Empty;
        public int Version { get; set; }

        public bool CanChange { get; set; }
        public bool CanPublish { get; set; }

        // For coaches: the request that is waiting for approval on this formation, if there is one
        public int? PendingRequestId { get; set; }

        public List<PlaySummaryDto> Plays { get; set; } = new List<PlaySummaryDto>();
    }

    public class PlaySummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DiagramJson { get; set; } = string.Empty;

        // True (for coaches) when a change to this play is waiting for approval
        public bool HasPendingChange { get; set; }
    }
}
