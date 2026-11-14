using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // A play drawn on top of a formation: the players plus their routes, blocks and motion.
    // This table holds approved content only, which is why players always see the last approved version.
    [Index(nameof(FormationId), nameof(Name), IsUnique = true)]
    public class Play
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Foreign key for the Formation entity
        public int FormationId { get; set; }
        public Formation? Formation { get; set; }

        [Required]
        [StringLength(60)]
        public string Name { get; set; } = string.Empty;

        // The drawing, as JSON (see Diagrams/DiagramDocument)
        [Required]
        public string DiagramJson { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Notes { get; set; }

        // Goes up by one with every approved change. A request made against an older version is refused.
        public int Version { get; set; } = 1;

        public DateTime UpdatedAt { get; set; }
    }
}
