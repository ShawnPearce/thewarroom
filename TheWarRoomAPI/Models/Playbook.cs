using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // A team has one playbook per unit: offence, defence and special teams.
    // Each game has its own three, because a formation has 11 players a side in one and 12 in the other.
    // The team works in the playbooks of the game it plays; the others are kept as they were left.
    [Index(nameof(TeamId), nameof(Unit), nameof(Rules), IsUnique = true)]
    public class Playbook
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        public TeamUnit Unit { get; set; }

        // The game this playbook is drawn for
        public FootballRules Rules { get; set; } = FootballRules.American;

        [Required]
        [StringLength(60)]
        public string Name { get; set; } = string.Empty;

        //Navigation property to represent the One-to-Many relationship with Formation
        public List<Formation>? Formations { get; set; }
    }
}
