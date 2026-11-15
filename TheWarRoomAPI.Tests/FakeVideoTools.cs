using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Stands in for ffmpeg and ffprobe: it answers what the test tells it to and remembers what it was asked
    public class FakeVideoTools : IVideoTools
    {
        public const string Mp4Family = "mov,mp4,m4a,3gp,3g2,mj2";

        public string? Problem { get; set; }

        // What a probe of an uploaded file answers: a one-minute H.264 video unless the test changes it
        public VideoFacts Facts { get; set; } = new VideoFacts
        {
            Container = Mp4Family,
            VideoCodec = "h264",
            Width = 1920,
            Height = 1080,
            DurationMs = 60_000,
            FrameCount = 1800
        };

        // True makes the next probe fail the way ffprobe does on a file that is not a video
        public bool Unreadable { get; set; }

        public List<string> Probed { get; } = new List<string>();

        public Task<VideoFacts> ProbeAsync(string input, CancellationToken cancellationToken)
        {
            Probed.Add(input);

            if (Unreadable)
            {
                throw new VideoToolException("This file could not be read as a video.");
            }

            return Task.FromResult(Facts);
        }

        // ----- Cutting and joining -----

        // Every cut that was asked for, in order: which input, from where, and how long
        public List<(string Input, int StartMs, int DurationMs)> Cuts { get; } = new List<(string, int, int)>();

        // How many times plays were joined into one video
        public int Joins { get; private set; }

        // Makes the cut with this number fail (1 is the first cut)
        public int? FailOnCut { get; set; }

        // Runs in the middle of a cut, so a test can change something while a cutup is being made
        public Action? WhileCutting { get; set; }

        public Task<VideoFacts> CutAsync(string input, int startMs, int durationMs, string outputPath, CancellationToken cancellationToken)
        {
            Cuts.Add((input, startMs, durationMs));

            if (FailOnCut == Cuts.Count)
            {
                throw new VideoToolException("A play could not be cut out of its video.");
            }

            WhileCutting?.Invoke();

            // The "video" is one byte holding the number of the cut, so a test can read the order of a joined file
            File.WriteAllBytes(outputPath, new[] { (byte)Cuts.Count });

            // Like the real tool, a cut ends on a whole picture at 30 pictures a second
            long frames = (long)Math.Round(durationMs * 30 / 1000.0);

            return Task.FromResult(new VideoFacts
            {
                Container = Mp4Family,
                VideoCodec = "h264",
                Width = 1280,
                Height = 720,
                FrameCount = frames,
                DurationMs = (int)Math.Round(frames * 1000 / 30.0)
            });
        }

        public Task JoinAsync(IReadOnlyList<string> partPaths, string outputPath, CancellationToken cancellationToken)
        {
            Joins++;
            File.WriteAllBytes(outputPath, partPaths.SelectMany(File.ReadAllBytes).ToArray());

            return Task.CompletedTask;
        }
    }
}
