using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // Makes a cutup into one video: every play is cut out of its own video in the same format,
    // the plays are joined in the cutup's order, and the result goes into film storage.
    //
    // The cutups that wait for this are simply the rows with the status Queued, so nothing is lost
    // when the API is stopped: at the next start the work carries on (see CutupRenderWorker).
    public class CutupRenderer
    {
        private readonly AppDbContext _context;
        private readonly IFilmStorage _storage;
        private readonly IVideoTools _tools;
        private readonly TimeProvider _clock;
        private readonly ILogger<CutupRenderer> _logger;

        public CutupRenderer(AppDbContext context, IFilmStorage storage, IVideoTools tools, TimeProvider clock, ILogger<CutupRenderer> logger)
        {
            _context = context;
            _storage = storage;
            _tools = tools;
            _clock = clock;
            _logger = logger;
        }

        // A cutup that was being made when the API stopped goes back in the queue, and is made again from the start
        public async Task<int> RequeueInterruptedAsync(CancellationToken cancellationToken)
        {
            return await _context.Cutups
                .Where(c => c.Status == CutupStatus.Rendering)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(c => c.Status, CutupStatus.Queued)
                    .SetProperty(c => c.ProgressPercent, 0), cancellationToken);
        }

        // Makes the video of the cutup that has waited longest. Returns false when none is waiting.
        public async Task<bool> RenderNextAsync(CancellationToken cancellationToken)
        {
            int? nextId = await _context.Cutups
                .Where(c => c.Status == CutupStatus.Queued)
                .OrderBy(c => c.QueuedAt)
                .ThenBy(c => c.Id)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (nextId == null)
            {
                return false;
            }

            // The cutup is taken only if it is still waiting at this very moment:
            // a coach may have turned it back into a draft since it was found.
            int taken = await _context.Cutups
                .Where(c => c.Id == nextId && c.Status == CutupStatus.Queued)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(c => c.Status, CutupStatus.Rendering)
                    .SetProperty(c => c.ProgressPercent, 0), cancellationToken);
            if (taken == 0)
            {
                return true;   // look again straight away
            }

            var cutup = await _context.Cutups
                .Include(c => c.Clips!)
                    .ThenInclude(p => p.Video)
                .FirstAsync(c => c.Id == nextId, cancellationToken);

            try
            {
                await RenderAsync(cutup, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;   // the API is stopping: the cutup stays as it is and is made again at the next start
            }
            catch (VideoToolException ex)
            {
                await FailAsync(cutup.Id, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The video of cutup {CutupId} could not be made", cutup.Id);
                await FailAsync(cutup.Id, "The video could not be made because something unexpected went wrong. Try again.");
            }

            return true;
        }

        private async Task RenderAsync(Cutup cutup, CancellationToken cancellationToken)
        {
            var plays = cutup.Clips!.OrderBy(p => p.Position).ToList();
            if (plays.Count == 0)
            {
                throw new VideoToolException("The cutup has no plays.");
            }

            // The parts are made in a folder of their own, which is removed again whatever happens
            string folder = Path.Combine(Path.GetTempPath(), "warroom-cutups", $"{cutup.Id}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);

            try
            {
                var parts = new List<string>();
                var frames = new List<long>();

                for (int i = 0; i < plays.Count; i++)
                {
                    var play = plays[i];
                    var video = play.Video!;

                    if (video.Status != VideoStatus.Ready || video.StorageKey == null)
                    {
                        throw new VideoToolException($"Play {play.Position} is cut from \"{video.Title}\", a video without a file.");
                    }

                    string part = Path.Combine(folder, $"play-{i + 1:000}.mp4");
                    var written = await _tools.CutAsync(
                        _storage.GetProcessingInput(video.StorageKey), play.StartMs, play.EndMs - play.StartMs, part, cancellationToken);

                    parts.Add(part);
                    frames.Add(written.FrameCount ?? MillisecondsToFrames(written.DurationMs));

                    // Cutting the plays is nearly all of the work; joining them is quick
                    await SetProgressAsync(cutup.Id, (i + 1) * 95 / plays.Count, cancellationToken);
                }

                string output = Path.Combine(folder, "cutup.mp4");
                await _tools.JoinAsync(parts, output, cancellationToken);

                // Business rule: every play starts where the one before it ends, measured on the video that was made.
                // A play is cut on whole pictures, so its real length differs a little from the length that was marked.
                var offsets = CutupTimeline.Offsets(frames.Select(FramesToMilliseconds));
                long allFrames = frames.Sum();

                string key = $"teams/{cutup.TeamId}/cutups/{Guid.NewGuid():N}.mp4";
                await _storage.PutFileAsync(key, output, "video/mp4", cancellationToken);

                // The file is stored, so from here on the record is brought in line with it whatever happens.
                // The check and the update below happen as one step.
                await using var transaction = await _context.Database.BeginTransactionAsync(CancellationToken.None);

                // A coach may have turned the cutup back into a draft, or deleted it, while it was being made.
                // Then the video is thrown away: the plays may no longer be the ones it was made from.
                var statusNow = await _context.Cutups
                    .AsNoTracking()
                    .Where(c => c.Id == cutup.Id)
                    .Select(c => (CutupStatus?)c.Status)
                    .FirstOrDefaultAsync(CancellationToken.None);
                if (statusNow != CutupStatus.Rendering)
                {
                    await _storage.DeleteAsync(key, CancellationToken.None);
                    return;
                }

                for (int i = 0; i < plays.Count; i++)
                {
                    plays[i].OutputOffsetMs = offsets[i];
                    plays[i].OutputDurationMs = FramesToMilliseconds(frames[i]);
                }

                cutup.StorageKey = key;
                cutup.DurationMs = FramesToMilliseconds(allFrames);
                cutup.ProgressPercent = 100;
                cutup.Status = CutupStatus.Ready;
                cutup.FailureReason = null;
                cutup.RenderedAt = _clock.GetUtcNow().UtcDateTime;

                await _context.SaveChangesAsync(CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);
            }
            finally
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (IOException)
                {
                    // Left for the system's own clean-up of temporary files
                }
            }
        }

        // The video could not be made: the cutup says why, and a coach can try again or change its plays
        private async Task FailAsync(int cutupId, string reason)
        {
            await _context.Cutups
                .Where(c => c.Id == cutupId && c.Status == CutupStatus.Rendering)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(c => c.Status, CutupStatus.Failed)
                    .SetProperty(c => c.ProgressPercent, 0)
                    .SetProperty(c => c.FailureReason, reason.Length <= 300 ? reason : reason.Substring(0, 300)));
        }

        private async Task SetProgressAsync(int cutupId, int percent, CancellationToken cancellationToken)
        {
            await _context.Cutups
                .Where(c => c.Id == cutupId && c.Status == CutupStatus.Rendering)
                .ExecuteUpdateAsync(update => update.SetProperty(c => c.ProgressPercent, percent), cancellationToken);
        }

        // Every play of a cutup is written at the same number of pictures a second
        private static int FramesToMilliseconds(long frames)
        {
            return (int)Math.Round(frames * 1000.0 / FfmpegVideoTools.OutputFramesPerSecond);
        }

        private static long MillisecondsToFrames(int milliseconds)
        {
            return (long)Math.Round(milliseconds * FfmpegVideoTools.OutputFramesPerSecond / 1000.0);
        }
    }
}
