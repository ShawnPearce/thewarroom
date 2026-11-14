using System.ComponentModel.DataAnnotations;


namespace TheWarRoomApp.Models
{
    public class ScheduledEventDto
    {
        public int Id { get; set; }
        [Display(Name = "Type")]
        public EventType EventType { get; set; }
        public string Title { get; set; } = string.Empty;
        [Display(Name = "Date")]
        public DateOnly EventDate { get; set; }
        [Display(Name = "Starts")]
        public TimeOnly StartTime { get; set; }
        [Display(Name = "Ends")]
        public TimeOnly? EndTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string? Notes { get; set; }

        // How many players have attendance recorded for this event
        public int AttendanceCount { get; set; }

        // True when the current user may change or delete the event, so the page can show the right links
        public bool CanEdit { get; set; }
    }
}
