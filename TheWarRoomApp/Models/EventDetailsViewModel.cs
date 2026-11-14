namespace TheWarRoomApp.Models
{
    // One event's page. A player also sees what was recorded for them at that event.
    public class EventDetailsViewModel
    {
        public ScheduledEventDto Event { get; set; } = new ScheduledEventDto();

        public AttendanceStatus? MyStatus { get; set; }
    }
}
