namespace TheWarRoomApp.Models
{
    // What the video player on a page plays
    public class FilmPlayerViewModel
    {
        // The address the video is loaded from
        public string Source { get; set; } = string.Empty;

        // Said to screen readers in place of the picture, for example "Full game, Riverside Raiders at Lakeshore Lions"
        public string Label { get; set; } = string.Empty;
    }
}
