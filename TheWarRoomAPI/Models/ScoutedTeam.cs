using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // A team that appears on film: the workspace's own team, or an opponent it scouts.
    // Games and play tags point at these, so film of two opponents playing each other
    // is kept the same way as film of the team's own games.
    [Index(nameof(TeamId), nameof(Name), IsUnique = true)]
    public class ScoutedTeam
    {
        public int Id { get; set; }

        // Foreign key for the Team entity (the workspace this record belongs to)
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // True for the one record that stands for the workspace's own team
        public bool IsOwnTeam { get; set; }
    }
}
