namespace TheWarRoomApp.Models
{
    // The depth chart page: the week picker, the chart of the chosen week, and a player's own spots
    public class DepthChartViewModel
    {
        public List<DepthChartWeekDto> Weeks { get; set; } = new List<DepthChartWeekDto>();

        // Empty when the team has no depth chart yet
        public DepthChartDto? Chart { get; set; }

        // The unit whose groups are shown
        public TeamUnit SelectedUnit { get; set; } = TeamUnit.Offence;

        // Filled for players only
        public List<MyPlacementDto> MyPlacements { get; set; } = new List<MyPlacementDto>();
    }
}
