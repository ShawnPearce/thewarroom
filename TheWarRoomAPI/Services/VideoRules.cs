namespace TheWarRoomAPI.Services
{
    // What an uploaded file has to be before it can be cut into plays.
    // Only MP4 and MOV files with H.264 video are taken, because those play in every browser.
    public static class VideoRules
    {
        public const int MinDurationMs = 1000;                      // one second
        public const int MaxDurationMs = 6 * 60 * 60 * 1000;        // six hours

        // The content type that goes with a file name, or null when that kind of file is not taken.
        // The type is decided here from the name; what the browser says about the file is not used.
        public static string? ContentTypeOf(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".mp4" => "video/mp4",
                ".m4v" => "video/mp4",
                ".mov" => "video/quicktime",
                _ => null
            };
        }

        // Empty when the video can be used; otherwise the reason it cannot, written for the coach who uploaded it
        public static string? WhyNotUsable(VideoFacts facts)
        {
            if (string.IsNullOrEmpty(facts.VideoCodec))
            {
                return "This file has no picture in it, so it is not a video.";
            }

            // ffprobe names the family of MP4 and MOV files "mov,mp4,m4a,3gp,3g2,mj2"
            var containers = facts.Container.Split(',');
            if (!containers.Contains("mp4") && !containers.Contains("mov"))
            {
                return "Only MP4 and MOV videos can be used.";
            }

            if (facts.VideoCodec != "h264")
            {
                return $"This video is stored as {facts.VideoCodec.ToUpperInvariant()}, which does not play in every browser. " +
                       "Export it as H.264 (often called \"most compatible\") and upload it again.";
            }

            if (facts.DurationMs < MinDurationMs)
            {
                return "This video is less than a second long.";
            }

            if (facts.DurationMs > MaxDurationMs)
            {
                return "This video is longer than six hours. Split it into shorter videos and upload them one at a time.";
            }

            return null;
        }
    }
}
