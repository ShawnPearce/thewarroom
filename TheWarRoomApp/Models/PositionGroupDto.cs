
namespace TheWarRoomApp.Models
{
    // A position group with its positions, coaches and players
    public class PositionGroupDto
    {
        public int Id { get; set; }
        public TeamUnit Unit { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }

        public List<PositionDto> Positions { get; set; } = new List<PositionDto>();
        public List<PersonDto> Coaches { get; set; } = new List<PersonDto>();
        public List<PersonDto> Players { get; set; } = new List<PersonDto>();

        // What the current user may do with this group, so the page can show the right buttons.
        // The API checks the same rules again when a change is sent.
        public bool CanManage { get; set; }
        public bool CanAssignCoaches { get; set; }
    }
}
