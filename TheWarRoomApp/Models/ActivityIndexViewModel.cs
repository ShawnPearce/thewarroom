namespace TheWarRoomApp.Models
{
    // The team's study time for one week
    public class ActivityIndexViewModel
    {
        public ActivityReportDto Report { get; set; } = new ActivityReportDto();

        // The Monday of the current week, so the page knows whether a later week exists
        public DateOnly ThisWeek { get; set; }
    }
}
