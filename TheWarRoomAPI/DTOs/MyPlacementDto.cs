using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // Where the signed-in player stands at one position in one week
    public class MyPlacementDto
    {
        public int WeekId { get; set; }
        public int PositionId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string PositionName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public TeamUnit Unit { get; set; }

        // 1 is the starter
        public int Rank { get; set; }

        // How many players are listed at this position
        public int PlayersAtPosition { get; set; }
    }
}
