using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // Which clips to list. Every filter is optional; together they narrow the list.
    public class ClipFilterDto
    {
        public int? GameId { get; set; }
        public int? VideoId { get; set; }

        // Several games at once; none of them means every game
        public List<int> GameIds { get; set; } = new List<int>();

        // Only tagged plays (true) or only plays without a tag (false)
        public bool? Tagged { get; set; }

        // The rest look at the tag, so they leave out plays that are not tagged
        public int? OffenceTeamId { get; set; }
        public PlayType? PlayType { get; set; }
        public int? Down { get; set; }
        public int? MinDistance { get; set; }
        public int? MaxDistance { get; set; }

        // A distance range by name, the same ranges the tendency charts use
        public DistanceRange? Distance { get; set; }
    }
}
