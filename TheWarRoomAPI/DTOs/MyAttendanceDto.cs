using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // One of the signed-in player's own attendance records
    public class MyAttendanceDto
    {
        public int EventId { get; set; }
        public EventType EventType { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateOnly EventDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public AttendanceStatus Status { get; set; }
    }
}
