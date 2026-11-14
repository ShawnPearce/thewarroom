namespace TheWarRoomAPI.DTOs
{
    public class DepthChartWeekDto
    {
        public int Id { get; set; }

        // The Monday of the week
        public DateOnly WeekStartDate { get; set; }

        public string? Label { get; set; }
    }
}
