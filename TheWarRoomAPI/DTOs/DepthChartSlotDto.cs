using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // One position in one week, with everything the editing page needs
    public class DepthChartSlotDto
    {
        public DepthChartWeekDto Week { get; set; } = new DepthChartWeekDto();
        public int PositionId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public TeamUnit Unit { get; set; }
        public bool CanEdit { get; set; }

        // In rank order: the first player is the starter
        public List<DepthChartPlayerDto> Players { get; set; } = new List<DepthChartPlayerDto>();

        // Players of the position's group who can still be added here
        public List<PersonDto> EligiblePlayers { get; set; } = new List<PersonDto>();
    }
}
