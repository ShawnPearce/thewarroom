
namespace TheWarRoomApp.Models
{
    // A position group as it appears inside other results (a coach's groups, a player's groups)
    public class GroupSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public TeamUnit Unit { get; set; }
    }
}
