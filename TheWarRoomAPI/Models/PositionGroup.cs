using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // A coaching group inside one unit, for example Offence / Receivers.
    // Coaches and players are assigned to groups, and each depth-chart position belongs to one.
    [Index(nameof(TeamId), nameof(Unit), nameof(Name), IsUnique = true)]
    public class PositionGroup
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        public TeamUnit Unit { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        //Navigation property to represent the One-to-Many relationship with Position
        public List<Position>? Positions { get; set; }

        //Navigation properties for the coaches and players assigned to this group
        public List<CoachGroupAssignment>? CoachAssignments { get; set; }
        public List<PlayerGroupAssignment>? PlayerAssignments { get; set; }
    }
}
