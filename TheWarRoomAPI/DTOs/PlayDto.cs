using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // A play as players and coaches see it: always the last approved version
    public class PlayDto
    {
        public int Id { get; set; }
        public int FormationId { get; set; }
        public string FormationName { get; set; } = string.Empty;
        public int PlaybookId { get; set; }
        public string PlaybookName { get; set; } = string.Empty;
        public TeamUnit Unit { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DiagramJson { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int Version { get; set; }
        public DateTime UpdatedAt { get; set; }

        public bool CanChange { get; set; }
        public bool CanPublish { get; set; }

        // For coaches: the request that is waiting for approval on this play, if there is one
        public int? PendingRequestId { get; set; }
    }
}
