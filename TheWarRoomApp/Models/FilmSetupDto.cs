namespace TheWarRoomApp.Models
{
    // Whether this server can take and process video, for the pages that offer an upload
    public class FilmSetupDto
    {
        // Where video files are kept, in words: "a folder on this computer", for example
        public string Storage { get; set; } = string.Empty;

        // Empty when video can be uploaded; otherwise what is missing, in words
        public string? Problem { get; set; }

        // The largest file that is taken, in bytes
        public long MaxUploadBytes { get; set; }
    }
}
