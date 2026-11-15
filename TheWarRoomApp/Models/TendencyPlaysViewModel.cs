namespace TheWarRoomApp.Models
{
    // The plays behind one tendency figure
    public class TendencyPlaysViewModel
    {
        public ScoutedTeamDto? Team { get; set; }
        public int GameCount { get; set; }

        public int? Down { get; set; }
        public DistanceRange? Distance { get; set; }

        // True for the list of tagged plays the charts leave out, because their down is not one the team's game has
        public bool LeftOut { get; set; }

        public List<ClipDto> Clips { get; set; } = new List<ClipDto>();

        // The query string that leads back to the tendencies page with the same filters
        public string BackQuery { get; set; } = string.Empty;
    }
}
