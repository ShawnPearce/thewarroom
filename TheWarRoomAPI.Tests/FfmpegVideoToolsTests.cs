using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // A test that runs the real ffmpeg programs. It is skipped on a computer where they are not installed.
    public sealed class FfmpegFactAttribute : FactAttribute
    {
        public FfmpegFactAttribute()
        {
            if (FfmpegVideoToolsTests.NewTools().Problem != null)
            {
                Skip = "ffmpeg is not installed on this computer.";
            }
        }
    }

    // Reading, cutting and joining real video files with ffmpeg and ffprobe.
    // The videos are test pictures that ffmpeg makes itself, a few seconds long.
    //
    // Where a number of pictures is checked, one more or less is accepted: which picture is the last one
    // of a cut differs by one between versions of ffmpeg, and the app measures what was made instead of assuming it.
    public class FfmpegVideoToolsTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "warroom-tests", Guid.NewGuid().ToString("N"));
        private readonly FfmpegVideoTools _tools = NewTools();

        public FfmpegVideoToolsTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        public static FfmpegVideoTools NewTools()
        {
            return new FfmpegVideoTools(Options.Create(new FilmOptions()), NullLogger<FfmpegVideoTools>.Instance);
        }

        // Makes a test video: moving colour bars at 30 pictures a second, with a tone as its sound
        private async Task<string> MakeVideoAsync(string fileName, int seconds, string size = "640x360", string codec = "libx264")
        {
            string path = Path.Combine(_folder, fileName);

            await RunFfmpegAsync(
                "-f", "lavfi", "-i", $"testsrc2=size={size}:rate=30:duration={seconds}",
                "-f", "lavfi", "-i", $"sine=frequency=440:duration={seconds}",
                "-c:v", codec, "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", path);

            return path;
        }

        private async Task RunFfmpegAsync(params string[] arguments)
        {
            var start = new ProcessStartInfo(_tools.FfmpegPath!) { RedirectStandardError = true, UseShellExecute = false };
            foreach (string argument in new[] { "-nostdin", "-hide_banner", "-loglevel", "error", "-y" }.Concat(arguments))
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)!;
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(process.ExitCode == 0, "ffmpeg could not make the test file: " + error);
        }

        [FfmpegFact]
        public async Task Probing_TellsWhatAVideoIs()
        {
            string video = await MakeVideoAsync("game.mp4", seconds: 4);

            var facts = await _tools.ProbeAsync(video, CancellationToken.None);

            Assert.Contains("mp4", facts.Container.Split(','));
            Assert.Equal("h264", facts.VideoCodec);
            Assert.Equal(640, facts.Width);
            Assert.Equal(360, facts.Height);
            Assert.InRange(facts.DurationMs, 3950, 4050);
            Assert.InRange(facts.FrameCount!.Value, 119, 121);   // 4 seconds at 30 pictures a second
            Assert.Null(VideoRules.WhyNotUsable(facts));
        }

        [FfmpegFact]
        public async Task AMovFile_IsReadTheSameWay()
        {
            string video = await MakeVideoAsync("phone.mov", seconds: 2);

            var facts = await _tools.ProbeAsync(video, CancellationToken.None);

            Assert.Contains("mov", facts.Container.Split(','));
            Assert.Null(VideoRules.WhyNotUsable(facts));
        }

        [FfmpegFact]
        public async Task AFileThatIsNotAVideo_CannotBeProbed()
        {
            string notes = Path.Combine(_folder, "notes.mp4");   // named like a video, but it is text
            await File.WriteAllTextAsync(notes, "First and ten, run left.");

            await Assert.ThrowsAsync<VideoToolException>(() => _tools.ProbeAsync(notes, CancellationToken.None));
            await Assert.ThrowsAsync<VideoToolException>(() => _tools.ProbeAsync(Path.Combine(_folder, "missing.mp4"), CancellationToken.None));
        }

        [FfmpegFact]
        public async Task ASoundFileNamedMp4_HasNoPicture()
        {
            string sound = Path.Combine(_folder, "sound.mp4");
            await RunFfmpegAsync("-f", "lavfi", "-i", "sine=frequency=440:duration=2", "-c:a", "aac", sound);

            var facts = await _tools.ProbeAsync(sound, CancellationToken.None);

            Assert.Null(facts.VideoCodec);
            Assert.Contains("no picture", VideoRules.WhyNotUsable(facts));
        }

        [FfmpegFact]
        public async Task AVideoThatIsNotH264_IsRecognised()
        {
            string video = await MakeVideoAsync("old.mp4", seconds: 2, codec: "mpeg4");

            var facts = await _tools.ProbeAsync(video, CancellationToken.None);

            Assert.Equal("mpeg4", facts.VideoCodec);
            Assert.Contains("MPEG4", VideoRules.WhyNotUsable(facts));
        }

        [FfmpegFact]
        public async Task PlaysAreCutInTheCutupFormat_AndJoinedInOrder()
        {
            string video = await MakeVideoAsync("game.mp4", seconds: 6);
            string first = Path.Combine(_folder, "play1.mp4");
            string second = Path.Combine(_folder, "play2.mp4");
            string cutup = Path.Combine(_folder, "cutup.mp4");

            // The later play goes first, as a coach might order a cutup
            var firstFacts = await _tools.CutAsync(video, startMs: 3500, durationMs: 2000, first, CancellationToken.None);
            var secondFacts = await _tools.CutAsync(video, startMs: 1000, durationMs: 1000, second, CancellationToken.None);

            foreach (var facts in new[] { firstFacts, secondFacts })
            {
                Assert.Equal("h264", facts.VideoCodec);
                Assert.Equal(FfmpegVideoTools.OutputWidth, facts.Width);
                Assert.Equal(FfmpegVideoTools.OutputHeight, facts.Height);
            }
            Assert.InRange(firstFacts.FrameCount!.Value, 59, 61);    // 2 seconds at 30 pictures a second
            Assert.InRange(secondFacts.FrameCount!.Value, 29, 31);

            await _tools.JoinAsync(new[] { first, second }, cutup, CancellationToken.None);

            var joined = await _tools.ProbeAsync(cutup, CancellationToken.None);
            Assert.Equal(firstFacts.FrameCount + secondFacts.FrameCount, joined.FrameCount);   // nothing lost and nothing added between the plays
            Assert.InRange(joined.DurationMs, 2900, 3100);
            Assert.Equal(FfmpegVideoTools.OutputWidth, joined.Width);
            Assert.False(File.Exists(cutup + ".parts.txt"));   // the list of parts is cleaned up
        }

        [FfmpegFact]
        public async Task AnUprightVideo_IsFittedIntoTheCutupFormatWithoutStretching()
        {
            string video = await MakeVideoAsync("upright.mp4", seconds: 2, size: "360x640");
            string play = Path.Combine(_folder, "play.mp4");

            var facts = await _tools.CutAsync(video, startMs: 0, durationMs: 1000, play, CancellationToken.None);

            Assert.Equal(FfmpegVideoTools.OutputWidth, facts.Width);
            Assert.Equal(FfmpegVideoTools.OutputHeight, facts.Height);
        }

        [FfmpegFact]
        public async Task CuttingPastTheEndOfAVideo_Fails()
        {
            string video = await MakeVideoAsync("game.mp4", seconds: 2);
            string play = Path.Combine(_folder, "play.mp4");

            // Nothing is left to cut 30 seconds into a 2-second video
            await Assert.ThrowsAsync<VideoToolException>(() => _tools.CutAsync(video, startMs: 30_000, durationMs: 1000, play, CancellationToken.None));
            Assert.False(File.Exists(play));   // the empty file ffmpeg wrote is not left behind

            // A play that runs past the end is cut as far as the video goes
            var facts = await _tools.CutAsync(video, startMs: 1500, durationMs: 5000, play, CancellationToken.None);
            Assert.InRange(facts.FrameCount!.Value, 13, 16);   // the half second that was left
        }

        [Fact]
        public void ProbeOutput_IsReadWhetherOrNotTheFileGivesEveryNumber()
        {
            // A video stream without its own length or picture count: the file's length is used
            var facts = FfmpegVideoTools.ReadFacts("""
                {
                  "streams": [
                    { "codec_type": "audio", "codec_name": "aac" },
                    { "codec_type": "video", "codec_name": "mjpeg", "width": 600, "height": 600, "disposition": { "attached_pic": 1 } },
                    { "codec_type": "video", "codec_name": "h264", "width": 1920, "height": 1080, "nb_frames": "N/A" }
                  ],
                  "format": { "format_name": "mov,mp4,m4a,3gp,3g2,mj2", "duration": "83.541000" }
                }
                """);

            Assert.Equal("h264", facts.VideoCodec);   // the cover picture is skipped
            Assert.Equal(1920, facts.Width);
            Assert.Equal(83_541, facts.DurationMs);
            Assert.Null(facts.FrameCount);

            // No streams at all
            var empty = FfmpegVideoTools.ReadFacts("""{ "format": { "format_name": "mp3", "duration": "12.5" } }""");
            Assert.Null(empty.VideoCodec);
            Assert.Equal(0, empty.DurationMs);
        }
    }
}
