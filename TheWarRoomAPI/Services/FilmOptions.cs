namespace TheWarRoomAPI.Services
{
    // The "Film" section of appsettings.json: where video files are kept and how they are processed
    public class FilmOptions
    {
        public const string Section = "Film";

        // "Local" keeps video files in a folder on this computer; "S3" keeps them in an Amazon S3 bucket
        public string Storage { get; set; } = "Local";

        // Local storage: the folder. A path that is not a full path starts at the API project's folder.
        public string LocalFolder { get; set; } = "film-storage";

        // S3 storage: the bucket and its region, for example "ca-central-1"
        public string? S3Bucket { get; set; }
        public string? S3Region { get; set; }

        // S3 storage: a named profile from the AWS credentials file on this computer.
        // When empty, the AWS SDK looks for credentials the usual way (environment variables, the default profile, the server's role).
        public string? S3Profile { get; set; }

        // The largest video that can be uploaded: 5 GB
        public long MaxUploadBytes { get; set; } = 5L * 1024 * 1024 * 1024;

        // Full paths of ffmpeg and ffprobe. When empty, they are looked for on the PATH.
        public string? FfmpegPath { get; set; }
        public string? FfprobePath { get; set; }
    }
}
