namespace TheWarRoomApp.Models
{
    // The schedule page: the events shown and the filters that produced them
    public class ScheduleViewModel
    {
        public List<ScheduledEventDto> Events { get; set; } = new List<ScheduledEventDto>();

        // "upcoming" or "past"
        public string Show { get; set; } = "upcoming";

        // Empty means every type
        public EventType? Type { get; set; }
    }
}
