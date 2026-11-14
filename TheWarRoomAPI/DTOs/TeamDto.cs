namespace TheWarRoomAPI.DTOs
{
    public class TeamDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool AttendanceTrackingEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
