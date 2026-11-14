
namespace TheWarRoomApp.Models
{
    // The attendance sheet of one event: every player with what has been recorded for them so far
    public class AttendanceSheetDto
    {
        public ScheduledEventDto Event { get; set; } = new ScheduledEventDto();

        // Whether the head coach has attendance tracking switched on for the team
        public bool TrackingEnabled { get; set; }

        // True when the current user may record attendance for this event right now
        public bool CanRecord { get; set; }

        // When CanRecord is false, the reason in words
        public string? CannotRecordReason { get; set; }

        public List<AttendanceRowDto> Rows { get; set; } = new List<AttendanceRowDto>();
    }

    public class AttendanceRowDto
    {
        public int PlayerId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? JerseyNumber { get; set; }

        // Empty when nothing has been recorded for the player yet
        public AttendanceStatus? Status { get; set; }

        public string? RecordedBy { get; set; }
        public DateTime? RecordedAt { get; set; }
    }
}
