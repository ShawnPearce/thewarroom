using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.Models
{
    // A practice, game or team meeting on the schedule
    public class ScheduledEvent
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        public EventType EventType { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        // The day and time are kept as the team wrote them (no time zone)
        public DateOnly EventDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }

        [Required]
        [StringLength(150)]
        public string Location { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }

        // Foreign key for the User entity (the coach who added the event)
        public int CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        //Navigation property to represent the One-to-Many relationship with AttendanceRecord
        public List<AttendanceRecord>? AttendanceRecords { get; set; }
    }
}
