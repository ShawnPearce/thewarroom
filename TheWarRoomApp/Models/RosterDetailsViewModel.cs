namespace TheWarRoomApp.Models
{
    // One player's page: roster details and the coaches who coach them
    public class RosterDetailsViewModel
    {
        public RosterEntryDto Player { get; set; } = new RosterEntryDto();

        // Empty when the viewer is not allowed to see this player's coaches
        public List<CoachSummaryDto>? Coaches { get; set; }
    }
}
