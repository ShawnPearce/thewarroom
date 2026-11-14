namespace TheWarRoomApp.Models
{
    // The tendencies page: the filters, what they can be chosen from, and the figures they produce
    public class TendenciesViewModel
    {
        // The team on offence
        public List<ScoutedTeamDto> Teams { get; set; } = new List<ScoutedTeamDto>();
        public ScoutedTeamDto? Team { get; set; }

        // The games that team played in, and the ones that are counted (none chosen means every game)
        public List<GameDto> Games { get; set; } = new List<GameDto>();
        public List<int> GameIds { get; set; } = new List<int>();

        // The situation: a down, a distance range, both, or neither
        public int? Down { get; set; }
        public DistanceRange? Distance { get; set; }

        // Empty when there is no team on film to show
        public TendencyReportDto? Report { get; set; }

        // The query string that keeps the chosen team and games and adds a situation to them.
        // It is used for the links to the plays behind a figure.
        public string Query(int? down, DistanceRange? distance)
        {
            var parts = new List<string>();

            if (Team != null)
            {
                parts.Add($"teamId={Team.Id}");
            }
            foreach (int gameId in GameIds)
            {
                parts.Add($"gameIds={gameId}");
            }
            if (down != null)
            {
                parts.Add($"down={down}");
            }
            if (distance != null)
            {
                parts.Add($"distance={distance}");
            }

            return string.Join("&", parts);
        }
    }
}
