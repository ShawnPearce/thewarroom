using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace TheWarRoomAPI.Services
{
    // Runs the ffmpeg and ffprobe programs, which have to be installed on the computer this API runs on.
    // Every value is passed as its own argument, never through a shell.
    public class FfmpegVideoTools : IVideoTools
    {
        // Every play of a cutup is written in exactly this format, so the plays can be joined without encoding them again
        public const int OutputWidth = 1280;
        public const int OutputHeight = 720;
        public const int OutputFramesPerSecond = 30;

        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan JoinTimeout = TimeSpan.FromMinutes(15);

        // Where the programs are usually installed, for when the PATH of this process does not include them
        private static readonly string[] UsualFolders = { "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin" };

        private readonly string? _ffmpeg;
        private readonly string? _ffprobe;
        private readonly ILogger<FfmpegVideoTools> _logger;

        public FfmpegVideoTools(IOptions<FilmOptions> options, ILogger<FfmpegVideoTools> logger)
        {
            _logger = logger;
            _ffmpeg = Find(options.Value.FfmpegPath, "ffmpeg");
            _ffprobe = Find(options.Value.FfprobePath, "ffprobe");

            if (_ffmpeg == null || _ffprobe == null)
            {
                Problem = "The ffmpeg programs were not found on this server. Video cannot be checked or made until ffmpeg is installed.";
            }
        }

        public string? Problem { get; }

        // Where the two programs were found; empty when they are not installed
        public string? FfmpegPath => _ffmpeg;
        public string? FfprobePath => _ffprobe;

        public async Task<VideoFacts> ProbeAsync(string input, CancellationToken cancellationToken)
        {
            var result = await RunAsync(Tool(_ffprobe), new[]
            {
                "-v", "error",
                "-print_format", "json",
                "-show_format",
                "-show_streams",
                input
            }, ProbeTimeout, cancellationToken);

            if (result.ExitCode != 0)
            {
                throw new VideoToolException("This file could not be read as a video.");
            }

            return ReadFacts(result.Output);
        }

        public async Task<VideoFacts> CutAsync(string input, int startMs, int durationMs, string outputPath, CancellationToken cancellationToken)
        {
            // Fit the picture inside 1280 x 720 without stretching it, fill the rest with black,
            // and bring it to 30 pictures a second
            string picture =
                $"scale={OutputWidth}:{OutputHeight}:force_original_aspect_ratio=decrease," +
                $"pad={OutputWidth}:{OutputHeight}:(ow-iw)/2:(oh-ih)/2," +
                $"fps={OutputFramesPerSecond},setsar=1,format=yuv420p";

            // Encoding is far quicker than playing, but a slow disk or network gets room
            var timeout = TimeSpan.FromMinutes(5) + TimeSpan.FromMilliseconds(durationMs * 20L);

            var result = await RunAsync(Tool(_ffmpeg), new[]
            {
                "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
                "-ss", Seconds(startMs),          // before the input: jumps there, then counts pictures exactly
                "-i", input,
                "-t", Seconds(durationMs),
                "-map", "0:v:0",                  // the picture only: cutups have no sound
                "-an", "-sn", "-dn",
                "-vf", picture,
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "23",
                "-movflags", "+faststart",
                outputPath
            }, timeout, cancellationToken);

            if (result.ExitCode != 0 || !File.Exists(outputPath))
            {
                _logger.LogWarning("ffmpeg could not cut {StartMs} ms + {DurationMs} ms: {Error}", startMs, durationMs, Tail(result.Error));
                throw new VideoToolException("A play could not be cut out of its video.");
            }

            // ffmpeg does not call it an error when the video ends before the play starts: it writes a file with no pictures.
            // So what was written is looked at, and measured for the caller at the same time.
            var written = await ProbeAsync(outputPath, cancellationToken);
            if (written.VideoCodec == null || written.DurationMs <= 0 || written.FrameCount == 0)
            {
                File.Delete(outputPath);
                throw new VideoToolException("A play could not be cut out of its video: the video ends before the play starts.");
            }

            return written;
        }

        public async Task JoinAsync(IReadOnlyList<string> partPaths, string outputPath, CancellationToken cancellationToken)
        {
            // ffmpeg reads the parts to join from a list file
            string listPath = outputPath + ".parts.txt";
            await File.WriteAllLinesAsync(listPath, partPaths.Select(p => $"file '{p.Replace("'", "'\\''")}'"), cancellationToken);

            try
            {
                var result = await RunAsync(Tool(_ffmpeg), new[]
                {
                    "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
                    "-f", "concat", "-safe", "0",
                    "-i", listPath,
                    "-c", "copy",                    // the parts already have the same format
                    "-movflags", "+faststart",       // lets a browser start playing before the whole file has arrived
                    outputPath
                }, JoinTimeout, cancellationToken);

                if (result.ExitCode != 0 || !File.Exists(outputPath))
                {
                    _logger.LogWarning("ffmpeg could not join {Count} parts: {Error}", partPaths.Count, Tail(result.Error));
                    throw new VideoToolException("The plays could not be joined into one video.");
                }
            }
            finally
            {
                File.Delete(listPath);
            }
        }

        // ---------- Helpers ----------

        // Reads the JSON that ffprobe prints
        public static VideoFacts ReadFacts(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var facts = new VideoFacts();
            double? formatSeconds = null;

            if (root.TryGetProperty("format", out var format))
            {
                facts.Container = Text(format, "format_name") ?? string.Empty;
                formatSeconds = Number(format, "duration");
            }

            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    // A cover picture inside a file counts as a "video" stream too, but it is not film
                    bool isCoverPicture = stream.TryGetProperty("disposition", out var disposition)
                        && disposition.TryGetProperty("attached_pic", out var attached)
                        && attached.ValueKind == JsonValueKind.Number && attached.GetInt32() == 1;

                    if (Text(stream, "codec_type") != "video" || isCoverPicture)
                    {
                        continue;
                    }

                    facts.VideoCodec = Text(stream, "codec_name");
                    facts.Width = (int)(Number(stream, "width") ?? 0);
                    facts.Height = (int)(Number(stream, "height") ?? 0);
                    facts.FrameCount = (long?)Number(stream, "nb_frames");

                    // The picture's own length when the file gives it; the whole file's length otherwise
                    double seconds = Number(stream, "duration") ?? formatSeconds ?? 0;
                    facts.DurationMs = (int)Math.Min(int.MaxValue, Math.Floor(seconds * 1000));
                    break;   // the first video stream is the film
                }
            }

            return facts;
        }

        private static string? Text(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        // ffprobe writes most numbers as text ("60.000000"), and "N/A" when it does not know
        private static double? Number(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.GetDouble();
            }

            if (value.ValueKind == JsonValueKind.String
                && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                return number;
            }

            return null;
        }

        // 12500 -> "12.500", always with a point
        private static string Seconds(int milliseconds)
        {
            return (milliseconds / 1000.0).ToString("0.000", CultureInfo.InvariantCulture);
        }

        private string Tool(string? path)
        {
            return path ?? throw new VideoToolException(Problem ?? "ffmpeg is not installed.");
        }

        // The configured path when there is one; otherwise the first match on the PATH or in the usual folders
        private static string? Find(string? configuredPath, string name)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return File.Exists(configuredPath) ? configuredPath : null;
            }

            var folders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Concat(UsualFolders);

            foreach (string folder in folders)
            {
                foreach (string fileName in new[] { name, name + ".exe" })
                {
                    string candidate = Path.Combine(folder, fileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private record ToolResult(int ExitCode, string Output, string Error);

        private static async Task<ToolResult> RunAsync(string program, IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var start = new ProcessStartInfo(program)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start) ?? throw new VideoToolException("The video tool could not be started.");

            // Both streams are read while the program runs, so it never waits on a full pipe
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();

            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(limit.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // it had already ended
                }

                cancellationToken.ThrowIfCancellationRequested();   // the API is shutting down, or the request was abandoned
                throw new VideoToolException("Working on the video took too long and was stopped.");
            }

            return new ToolResult(process.ExitCode, await output, await error);
        }

        // The end of what the tool wrote, for the log.
        // A signed link is cut off at its "?", because what follows would let anyone read the file.
        private static string Tail(string text)
        {
            text = Regex.Replace(text.Trim(), @"(https?://[^\s?'""]+)\?[^\s'""]*", "$1?...");
            return text.Length <= 500 ? text : text.Substring(text.Length - 500);
        }
    }
}
