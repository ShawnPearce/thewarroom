namespace TheWarRoomAPI.Services
{
    // Reading and making video files. The work is done by the ffmpeg and ffprobe programs (see FfmpegVideoTools).
    public interface IVideoTools
    {
        // Empty when the tools are installed; otherwise what is missing, in words
        string? Problem { get; }

        // What a video file is: its container, codec, picture size and length.
        // Throws VideoToolException when the file cannot be read as a video at all.
        Task<VideoFacts> ProbeAsync(string input, CancellationToken cancellationToken);

        // Writes one play of a video (from startMs, durationMs long) as a file of its own in the cutup format:
        // 1280 x 720, 30 pictures a second, H.264, no sound. Answers with what was written, measured.
        // Throws VideoToolException when the play could not be cut, or nothing was left at that time.
        Task<VideoFacts> CutAsync(string input, int startMs, int durationMs, string outputPath, CancellationToken cancellationToken);

        // Joins parts made by CutAsync into one video, in the order given, without encoding them again
        Task JoinAsync(IReadOnlyList<string> partPaths, string outputPath, CancellationToken cancellationToken);
    }

    // What ffprobe found out about a file
    public class VideoFacts
    {
        // The container as ffprobe names it, for example "mov,mp4,m4a,3gp,3g2,mj2"
        public string Container { get; set; } = string.Empty;

        // Empty when the file has no picture
        public string? VideoCodec { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }

        // The length of the picture in milliseconds
        public int DurationMs { get; set; }

        // How many pictures the video has, when the file says so
        public long? FrameCount { get; set; }
    }

    // ffmpeg or ffprobe could not do what was asked. The message is written for the person who uploaded the file.
    public class VideoToolException : Exception
    {
        public VideoToolException(string message) : base(message)
        {
        }
    }
}
