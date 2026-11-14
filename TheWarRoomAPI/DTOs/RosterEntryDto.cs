using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // One player on the roster
    public class RosterEntryDto
    {
        public int PlayerId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // Left empty when a player is looking at the roster
        public string? Email { get; set; }

        public int? JerseyNumber { get; set; }
        public int? Grade { get; set; }
        public int? HeightInches { get; set; }
        public int? WeightLbs { get; set; }
        public RosterStatus Status { get; set; }
        public bool IsActive { get; set; }

        // The position groups the player belongs to
        public List<GroupSummaryDto> Groups { get; set; } = new List<GroupSummaryDto>();
    }
}
