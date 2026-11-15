using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // One slot on the depth chart, for example X, QB or MLB
    [Index(nameof(TeamId), nameof(Code), IsUnique = true)]
    public class Position
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Foreign key for the PositionGroup entity
        public int PositionGroupId { get; set; }

        //Navigation property to represent the relationship with PositionGroup
        public PositionGroup? PositionGroup { get; set; }

        // Short label shown on the chart, unique within the team
        [Required]
        [StringLength(6)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        // Empty for a position both games have. A position only one game has (the tight end in American football,
        // the slotbacks in Canadian football) names that game, and is put away while the team plays the other one.
        public FootballRules? Rules { get; set; }

        public int SortOrder { get; set; }
    }
}
