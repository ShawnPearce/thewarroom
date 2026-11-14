namespace TheWarRoomApp.Models
{
    // The new-week form, with the earlier weeks it can be copied from
    public class NewWeekViewModel
    {
        public CreateDepthChartWeekDto Week { get; set; } = new CreateDepthChartWeekDto();

        public List<DepthChartWeekDto> Weeks { get; set; } = new List<DepthChartWeekDto>();
    }
}
