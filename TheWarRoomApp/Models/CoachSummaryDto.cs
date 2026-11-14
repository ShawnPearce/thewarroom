namespace TheWarRoomApp.Models
{
    // One of a player's coaches, with the reason they coach that player
    public class CoachSummaryDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // For example "Head coach", "Offence coordinator" or "Receivers coach"
        public string Title { get; set; } = string.Empty;
    }
}
