using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // The full depth chart of one week: groups, their positions, and the players in order
    public class DepthChartDto
    {
        public DepthChartWeekDto Week { get; set; } = new DepthChartWeekDto();
        public List<DepthChartGroupDto> Groups { get; set; } = new List<DepthChartGroupDto>();

        // What the current user may do, so the page can show the right buttons
        public bool CanCreateWeek { get; set; }
        public bool CanDeleteWeek { get; set; }
    }

    public class DepthChartGroupDto
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public TeamUnit Unit { get; set; }

        // True when the current user may change the order at this group's positions
        public bool CanEdit { get; set; }

        public List<DepthChartPositionDto> Positions { get; set; } = new List<DepthChartPositionDto>();
    }

    public class DepthChartPositionDto
    {
        public int PositionId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        // In rank order: the first player is the starter
        public List<DepthChartPlayerDto> Players { get; set; } = new List<DepthChartPlayerDto>();
    }

    public class DepthChartPlayerDto
    {
        public int PlacementId { get; set; }
        public int PlayerId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? JerseyNumber { get; set; }
        public int Rank { get; set; }
        public RosterStatus Status { get; set; }
        public bool IsActive { get; set; }
    }
}
