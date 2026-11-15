using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    public class TeamDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool AttendanceTrackingEnabled { get; set; }

        // The game the team plays, and how many players a side that puts on the field (11 or 12)
        public FootballRules Rules { get; set; }
        public int PlayersPerSide { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
