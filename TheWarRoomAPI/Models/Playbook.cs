using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // A team has one playbook per unit: offence, defence and special teams
    [Index(nameof(TeamId), nameof(Unit), IsUnique = true)]
    public class Playbook
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        public TeamUnit Unit { get; set; }

        [Required]
        [StringLength(60)]
        public string Name { get; set; } = string.Empty;

        //Navigation property to represent the One-to-Many relationship with Formation
        public List<Formation>? Formations { get; set; }
    }
}
