using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    public class PositionDto
    {
        public int Id { get; set; }
        public int PositionGroupId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }

        // Empty for a position both games have; otherwise the one game that has it
        public FootballRules? Rules { get; set; }

        // True when the position belongs only to the game the team is not playing now.
        // It is then left off the depth chart, with the players listed at it, until the team plays that game again.
        public bool IsPutAway { get; set; }
    }
}
