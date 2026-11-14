
namespace TheWarRoomApp.Models
{
    // What a coach sends when saving an attendance sheet
    public class RecordAttendanceDto
    {
        public List<AttendanceEntryDto> Entries { get; set; } = new List<AttendanceEntryDto>();
    }

    public class AttendanceEntryDto
    {
        public int PlayerId { get; set; }

        // Leaving the status empty removes what was recorded for the player
        public AttendanceStatus? Status { get; set; }
    }
}
