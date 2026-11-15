namespace TheWarRoomApp.Models
{
    // The plays of a video, as the list on the video's page
    public class PlayListViewModel
    {
        public VideoDetailDto Video { get; set; } = new VideoDetailDto();

        // True when the video can be played on the page: a play is then opened in the editor beside the player.
        // False for a record without a file: its plays are edited on the form where times are typed.
        public bool OnTheVideo { get; set; }
    }
}
