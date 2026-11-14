namespace TheWarRoomApp.Models
{
    // The page where plays are chosen for a cutup: pick a game, narrow its plays, tick the ones to add
    public class CutupAddPlaysViewModel
    {
        public CutupDetailDto Cutup { get; set; } = new CutupDetailDto();

        public List<GameDto> Games { get; set; } = new List<GameDto>();
        public GameDto? Game { get; set; }

        // The filters, which are all optional
        public int? OffenceTeamId { get; set; }
        public PlayType? PlayType { get; set; }
        public int? Down { get; set; }

        // The plays of the chosen game that match the filters
        public List<ClipDto> Clips { get; set; } = new List<ClipDto>();
    }
}
