namespace TheWarRoomApp.Models
{
    // A game with its videos, and whether this server can take another video
    public class FilmGameViewModel
    {
        public GameDetailDto Game { get; set; } = new GameDetailDto();
        public FilmSetupDto Setup { get; set; } = new FilmSetupDto();
    }
}
