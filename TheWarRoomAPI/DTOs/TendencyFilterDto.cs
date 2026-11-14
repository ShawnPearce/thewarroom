using System.ComponentModel.DataAnnotations;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // Which tagged plays to count. Every part is optional; together they narrow the plays.
    public class TendencyFilterDto
    {
        // One game, several games, or none of them for every game on film
        public List<int> GameIds { get; set; } = new List<int>();

        // The team that has the ball (a team on film)
        public int? OffenceTeamId { get; set; }

        [Range(1, 4, ErrorMessage = "The down must be between 1 and 4.")]
        public int? Down { get; set; }

        public DistanceRange? Distance { get; set; }
    }
}
