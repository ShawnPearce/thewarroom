using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // One player's attendance at one event. There is at most one record per player per event.
    [Index(nameof(ScheduledEventId), nameof(PlayerId), IsUnique = true)]
    public class AttendanceRecord
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Foreign key for the ScheduledEvent entity
        public int ScheduledEventId { get; set; }
        public ScheduledEvent? ScheduledEvent { get; set; }

        // Foreign key for the User entity (the player)
        public int PlayerId { get; set; }
        public User? Player { get; set; }

        public AttendanceStatus Status { get; set; }

        // Foreign key for the User entity (the coach who recorded it)
        public int RecordedById { get; set; }
        public User? RecordedBy { get; set; }

        public DateTime RecordedAt { get; set; }
    }
}
