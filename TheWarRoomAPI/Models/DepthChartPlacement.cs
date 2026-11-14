using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // One player's spot at one position in one week. Rank 1 is the starter.
    // A player can have placements at several positions, each with its own rank.
    [Index(nameof(DepthChartWeekId), nameof(PositionId), nameof(PlayerId), IsUnique = true)]
    public class DepthChartPlacement
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Foreign key for the DepthChartWeek entity
        public int DepthChartWeekId { get; set; }
        public DepthChartWeek? DepthChartWeek { get; set; }

        // Foreign key for the Position entity
        public int PositionId { get; set; }
        public Position? Position { get; set; }

        // Foreign key for the User entity (the player)
        public int PlayerId { get; set; }
        public User? Player { get; set; }

        public int Rank { get; set; }
    }
}
