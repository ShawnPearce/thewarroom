using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // A formation: where the players line up. Plays are organised under formations.
    // This table holds approved content only; a change that is waiting for approval lives in ApprovalRequest.
    [Index(nameof(PlaybookId), nameof(Name), IsUnique = true)]
    public class Formation
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Foreign key for the Playbook entity
        public int PlaybookId { get; set; }
        public Playbook? Playbook { get; set; }

        [Required]
        [StringLength(60)]
        public string Name { get; set; } = string.Empty;

        // The drawing, as JSON (see Diagrams/DiagramDocument)
        [Required]
        public string DiagramJson { get; set; } = string.Empty;

        // Goes up by one with every approved change. A request made against an older version is refused.
        public int Version { get; set; } = 1;

        public DateTime UpdatedAt { get; set; }

        //Navigation property to represent the One-to-Many relationship with Play
        public List<Play>? Plays { get; set; }
    }
}
