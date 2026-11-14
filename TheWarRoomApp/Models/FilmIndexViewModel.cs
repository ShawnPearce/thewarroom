namespace TheWarRoomApp.Models
{
    // The games on film, with the team filter that produced the list
    public class FilmIndexViewModel
    {
        public List<GameDto> Games { get; set; } = new List<GameDto>();
        public List<ScoutedTeamDto> Teams { get; set; } = new List<ScoutedTeamDto>();

        // Empty means every team
        public int? TeamId { get; set; }
    }
}
