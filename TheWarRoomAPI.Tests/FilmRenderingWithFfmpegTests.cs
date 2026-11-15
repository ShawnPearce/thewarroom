using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The whole way with nothing stood in for: a real video in a real folder is checked by ffprobe,
    // cut into plays, and made into one cutup by ffmpeg. Skipped where ffmpeg is not installed.
    public class FilmRenderingWithFfmpegTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "warroom-tests", Guid.NewGuid().ToString("N"));

        private readonly FfmpegVideoTools _tools = FfmpegVideoToolsTests.NewTools();
        private readonly LocalFilmStorage _storage;
        private readonly IOptions<FilmOptions> _options;

        public FilmRenderingWithFfmpegTests()
        {
            Directory.CreateDirectory(_folder);
            _options = Options.Create(new FilmOptions { LocalFolder = Path.Combine(_folder, "film-storage") });
            _storage = new LocalFilmStorage(_options, new Environment(_folder));
        }

        public void Dispose()
        {
            _world.Dispose();
            Directory.Delete(_folder, recursive: true);
        }

        [FfmpegFact]
        public async Task AnUploadedVideo_IsCutIntoPlays_AndMadeIntoOneCutup()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            // A ten-second game video, as a coach's computer would have it
            string source = Path.Combine(_folder, "game.mp4");
            await RunFfmpegAsync(
                "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=30:duration=10",
                "-f", "lavfi", "-i", "sine=frequency=440:duration=10",
                "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", source);

            using var context = _world.NewContext();
            var videos = new VideoService(context, _world.Clips(context), _storage, _tools, _options, _world.Clock);
            var cutups = new CutupService(context, _storage, _tools, _world.Clock);
            var renderer = new CutupRenderer(context, _storage, _tools, _world.Clock, NullLogger<CutupRenderer>.Instance);

            // Upload: start, send the file, complete
            var ticket = (await videos.StartUploadAsync(headCoach, new StartUploadDto
            {
                GameId = film.GameId,
                Title = "Sideline camera",
                FileName = "game.mp4",
                SizeBytes = new FileInfo(source).Length
            }))!;
            await using (var file = File.OpenRead(source))
            {
                await videos.ReceiveFileAsync(headCoach, ticket.VideoId, file, CancellationToken.None);
            }
            var uploaded = await videos.CompleteUploadAsync(headCoach, ticket.VideoId, CancellationToken.None);

            Assert.Equal(VideoStatus.Ready, uploaded!.Status);
            Assert.InRange(uploaded.DurationMs!.Value, 9_950, 10_050);   // ffprobe measured the ten seconds

            // Two plays, put in the cutup with the later one first
            var clips = _world.Clips(context);
            var early = await clips.CreateAsync(headCoach, new SaveClipDto { VideoId = ticket.VideoId, StartMs = 1_000, EndMs = 3_000 });
            var late = await clips.CreateAsync(headCoach, new SaveClipDto { VideoId = ticket.VideoId, StartMs = 6_500, EndMs = 9_000 });

            var cutup = await cutups.CreateAsync(headCoach, new SaveCutupDto { Name = "Two plays" });
            await cutups.AddClipsAsync(headCoach, cutup.Id, new AddCutupClipsDto { ClipIds = new List<int> { late!.Id, early!.Id } });

            Assert.True(await cutups.RequestRenderAsync(headCoach, cutup.Id));
            Assert.True(await renderer.RenderNextAsync(CancellationToken.None));

            var made = await context.Cutups.AsNoTracking().Include(c => c.Clips).SingleAsync(c => c.Id == cutup.Id);
            Assert.Equal(CutupStatus.Ready, made.Status);
            Assert.Null(made.FailureReason);

            // 2.5 seconds, then 2 seconds, each measured on what was made (a picture more or less, depending on the ffmpeg version).
            // The second play starts exactly where the first one ends.
            var plays = made.Clips!.OrderBy(p => p.Position).ToList();
            int firstLength = plays[0].OutputDurationMs!.Value;
            int secondLength = plays[1].OutputDurationMs!.Value;
            Assert.InRange(firstLength, 2_460, 2_540);
            Assert.InRange(secondLength, 1_960, 2_040);
            Assert.Equal(0, plays[0].OutputOffsetMs);
            Assert.Equal(firstLength, plays[1].OutputOffsetMs);
            Assert.InRange(made.DurationMs!.Value, firstLength + secondLength - 1, firstLength + secondLength + 1);

            // The file in storage really is that video: in the cutup format, with every picture of both plays
            var file2 = await cutups.GetFileAsync(headCoach, cutup.Id);
            var facts = await _tools.ProbeAsync(file2!.Path!, CancellationToken.None);
            Assert.Equal("h264", facts.VideoCodec);
            Assert.Equal((FfmpegVideoTools.OutputWidth, FfmpegVideoTools.OutputHeight), (facts.Width, facts.Height));
            Assert.Equal((long)Math.Round(made.DurationMs.Value * 30 / 1000.0), facts.FrameCount!.Value);
            Assert.InRange(facts.DurationMs, made.DurationMs.Value - 50, made.DurationMs.Value + 50);

            // Nothing is left over from the work, and the cutup's video goes when the cutup does
            Assert.Empty(Directory.GetDirectories(Path.Combine(Path.GetTempPath(), "warroom-cutups"), $"{cutup.Id}-*"));
            Assert.True(await cutups.DeleteAsync(headCoach, cutup.Id));
            Assert.False(File.Exists(file2.Path));
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

        // The least a storage needs to know about where the API runs
        private class Environment : IHostEnvironment
        {
            public Environment(string contentRoot)
            {
                ContentRootPath = contentRoot;
            }

            public string EnvironmentName { get; set; } = "Test";
            public string ApplicationName { get; set; } = "TheWarRoomAPI.Tests";
            public string ContentRootPath { get; set; }
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
