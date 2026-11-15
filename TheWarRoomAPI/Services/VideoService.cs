using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class VideoService : IVideoService
    {
        // A signed link to send a file to must be used within the hour; one to play from lasts a long film session
        private static readonly TimeSpan UploadLinkLife = TimeSpan.FromHours(1);
        private static readonly TimeSpan PlaybackLinkLife = TimeSpan.FromHours(6);

        private readonly AppDbContext _context;
        private readonly IClipService _clipService;
        private readonly IFilmStorage _storage;
        private readonly IVideoTools _tools;
        private readonly FilmOptions _options;
        private readonly TimeProvider _clock;

        public VideoService(AppDbContext context, IClipService clipService, IFilmStorage storage, IVideoTools tools,
            IOptions<FilmOptions> options, TimeProvider clock)
        {
            _context = context;
            _clipService = clipService;
            _storage = storage;
            _tools = tools;
            _options = options.Value;
            _clock = clock;
        }

        public FilmSetupDto GetSetup(CurrentUser currentUser)
        {
            FilmRules.EnsureCoach(currentUser);

            return new FilmSetupDto
            {
                Storage = _storage.Description,
                Problem = _storage.Problem ?? _tools.Problem,
                MaxUploadBytes = _options.MaxUploadBytes
            };
        }

        public async Task<VideoDetailDto?> GetVideoAsync(CurrentUser currentUser, int id)
        {
            FilmRules.EnsureCoach(currentUser);

            int downs = FilmRules.Downs(currentUser.Rules);

            var video = await _context.Videos
                .Where(v => v.Id == id && v.TeamId == currentUser.TeamId)
                .Select(v => new VideoDetailDto
                {
                    Downs = downs,
                    Id = v.Id,
                    GameId = v.GameId,
                    Title = v.Title,
                    Status = v.Status,
                    DurationMs = v.DurationMs,
                    HasFile = v.Status == VideoStatus.Ready && v.StorageKey != null,
                    FileName = v.OriginalFileName,
                    SizeBytes = v.SizeBytes,
                    FailureReason = v.FailureReason,
                    ClipCount = v.Clips!.Count,
                    TaggedClipCount = v.Clips!.Count(c => c.Tag != null),
                    UploadedBy = v.UploadedBy!.FirstName + " " + v.UploadedBy.LastName,
                    CreatedAt = v.CreatedAt,
                    GameDate = v.Game!.GameDate,
                    GameLabel = v.Game.Label,
                    HomeTeamId = v.Game.HomeTeamId,
                    HomeTeamName = v.Game.HomeTeam!.Name,
                    AwayTeamId = v.Game.AwayTeamId,
                    AwayTeamName = v.Game.AwayTeam!.Name
                })
                .FirstOrDefaultAsync();
            if (video == null)
            {
                return null;
            }

            video.Clips = await _clipService.GetClipsAsync(currentUser, new ClipFilterDto { VideoId = video.Id });
            return video;
        }

        // ---------- Uploading ----------

        public async Task<UploadTicketDto?> StartUploadAsync(CurrentUser currentUser, StartUploadDto dto)
        {
            FilmRules.EnsureCoach(currentUser);

            var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == dto.GameId && g.TeamId == currentUser.TeamId);
            if (game == null)
            {
                return null;
            }

            // Business rule: nothing is uploaded that this server could not check or play back afterwards
            string? problem = _storage.Problem ?? _tools.Problem;
            if (problem != null)
            {
                throw new InvalidOperationException(problem);
            }

            // Business rule: only MP4 and MOV files are taken, up to the largest size
            string fileName = NameWithoutFolders(dto.FileName);
            string? contentType = VideoRules.ContentTypeOf(fileName);
            if (contentType == null)
            {
                throw new InvalidOperationException("Only MP4 and MOV videos can be uploaded.");
            }

            if (dto.SizeBytes > _options.MaxUploadBytes)
            {
                throw new InvalidOperationException($"The file is larger than {Labels.Size(_options.MaxUploadBytes)}, the most that can be uploaded.");
            }

            // Map DTO → entity. The server makes up where the file goes: the team's folder and a name nobody can guess.
            var video = new Video
            {
                TeamId = currentUser.TeamId,
                GameId = game.Id,
                Title = dto.Title.Trim(),
                Status = VideoStatus.Uploading,
                StorageKey = $"teams/{currentUser.TeamId}/videos/{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}",
                OriginalFileName = fileName,
                ContentType = contentType,
                SizeBytes = dto.SizeBytes,
                UploadedById = currentUser.Id,
                CreatedAt = _clock.GetUtcNow().UtcDateTime
            };

            _context.Videos.Add(video);
            await _context.SaveChangesAsync();

            return new UploadTicketDto
            {
                VideoId = video.Id,
                ContentType = contentType,
                UploadUrl = _storage.UsesSignedLinks
                    ? _storage.CreateUploadLink(video.StorageKey, contentType, UploadLinkLife)
                    : null
            };
        }

        public async Task<bool> ReceiveFileAsync(CurrentUser currentUser, int id, Stream content, CancellationToken cancellationToken)
        {
            FilmRules.EnsureCoach(currentUser);

            var video = await _context.Videos.FirstOrDefaultAsync(v => v.Id == id && v.TeamId == currentUser.TeamId);
            if (video == null)
            {
                return false;
            }

            if (_storage.UsesSignedLinks)
            {
                throw new InvalidOperationException("Video files are sent straight to film storage, not through the site.");
            }

            if (video.Status != VideoStatus.Uploading || video.StorageKey == null)
            {
                throw new InvalidOperationException("This video is not waiting for a file.");
            }

            await _storage.SaveAsync(video.StorageKey, content, _options.MaxUploadBytes, cancellationToken);

            // The record may have been deleted while the file was arriving; then the file goes too
            bool stillThere = await _context.Videos.AnyAsync(v => v.Id == id);
            if (!stillThere)
            {
                await _storage.DeleteAsync(video.StorageKey, CancellationToken.None);
                return false;
            }

            return true;
        }

        public async Task<VideoDto?> CompleteUploadAsync(CurrentUser currentUser, int id, CancellationToken cancellationToken)
        {
            FilmRules.EnsureCoach(currentUser);

            var video = await _context.Videos
                .Include(v => v.UploadedBy)
                .FirstOrDefaultAsync(v => v.Id == id && v.TeamId == currentUser.TeamId);
            if (video == null)
            {
                return null;
            }

            if (video.Status == VideoStatus.Ready)
            {
                return ToDto(video);   // asked twice: the first answer stands
            }

            if ((video.Status != VideoStatus.Uploading && video.Status != VideoStatus.Processing) || video.StorageKey == null)
            {
                throw new InvalidOperationException("This upload cannot be finished. Delete the video and upload the file again.");
            }

            long? size = await _storage.GetSizeAsync(video.StorageKey, cancellationToken);
            if (size == null)
            {
                throw new InvalidOperationException("The file has not arrived in film storage. Upload it again.");
            }

            if (size > _options.MaxUploadBytes)
            {
                return await FailAsync(video, $"The file is larger than {Labels.Size(_options.MaxUploadBytes)}, the most that can be uploaded.");
            }

            video.SizeBytes = size;
            video.Status = VideoStatus.Processing;
            await _context.SaveChangesAsync();

            // Business rule: the server looks at the file itself. What the browser said about it is not trusted.
            VideoFacts facts;
            try
            {
                facts = await _tools.ProbeAsync(_storage.GetProcessingInput(video.StorageKey), cancellationToken);
            }
            catch (VideoToolException ex)
            {
                return await FailAsync(video, ex.Message);
            }

            string? refusal = VideoRules.WhyNotUsable(facts);
            if (refusal != null)
            {
                return await FailAsync(video, refusal);
            }

            // The length is known now, so plays can be cut and checked against it
            video.DurationMs = facts.DurationMs;
            video.Status = VideoStatus.Ready;
            video.FailureReason = null;
            await _context.SaveChangesAsync();

            return ToDto(video);
        }

        // ---------- The file ----------

        public async Task<FilmFile?> GetFileAsync(CurrentUser currentUser, int id)
        {
            FilmRules.EnsureCoach(currentUser);

            var video = await _context.Videos.FirstOrDefaultAsync(v => v.Id == id && v.TeamId == currentUser.TeamId);
            if (video == null || video.Status != VideoStatus.Ready || video.StorageKey == null)
            {
                return null;
            }

            string contentType = video.ContentType ?? "video/mp4";

            if (_storage.UsesSignedLinks)
            {
                return new FilmFile { SignedLink = _storage.CreatePlaybackLink(video.StorageKey, PlaybackLinkLife), ContentType = contentType };
            }

            string? path = _storage.FindFile(video.StorageKey);
            return path == null ? null : new FilmFile { Path = path, ContentType = contentType };
        }

        public async Task<bool> DeleteAsync(CurrentUser currentUser, int id, CancellationToken cancellationToken)
        {
            FilmRules.EnsureCoach(currentUser);

            var video = await _context.Videos.FirstOrDefaultAsync(v => v.Id == id && v.TeamId == currentUser.TeamId);
            if (video == null)
            {
                return false;
            }

            // Business rule: a video whose plays are used in a cutup stays, the same way a single play does
            var usedIn = await _context.CutupClips
                .Where(p => p.VideoId == video.Id)
                .Select(p => p.Cutup!.Name)
                .FirstOrDefaultAsync();
            if (usedIn != null)
            {
                throw new InvalidOperationException($"Plays of this video are used in the cutup \"{usedIn}\". Take them out of the cutup first.");
            }

            string? key = video.StorageKey;

            _context.Videos.Remove(video);   // its plays and their tags are deleted with it
            await _context.SaveChangesAsync();

            // The file goes after the record, so a record never points at a file that is gone
            if (key != null)
            {
                await _storage.DeleteAsync(key, cancellationToken);
            }

            return true;
        }

        // ---------- Helpers ----------

        // The upload could not be used: the file is thrown away and the record says why
        private async Task<VideoDto?> FailAsync(Video video, string reason)
        {
            if (video.StorageKey != null)
            {
                await _storage.DeleteAsync(video.StorageKey, CancellationToken.None);
            }

            video.StorageKey = null;
            video.Status = VideoStatus.Failed;
            video.FailureReason = reason.Length <= 300 ? reason : reason.Substring(0, 300);
            await _context.SaveChangesAsync();

            throw new InvalidOperationException(reason);
        }

        // "C:\Film\week3.mp4" -> "week3.mp4"
        private static string NameWithoutFolders(string fileName)
        {
            fileName = fileName.Trim();
            int lastSeparator = fileName.LastIndexOfAny(new[] { '/', '\\' });

            return lastSeparator < 0 ? fileName : fileName.Substring(lastSeparator + 1);
        }

        // Map entity → DTO (the video must be loaded with the coach who uploaded it)
        private static VideoDto ToDto(Video video)
        {
            return new VideoDto
            {
                Id = video.Id,
                GameId = video.GameId,
                Title = video.Title,
                Status = video.Status,
                DurationMs = video.DurationMs,
                HasFile = video.Status == VideoStatus.Ready && video.StorageKey != null,
                FileName = video.OriginalFileName,
                SizeBytes = video.SizeBytes,
                FailureReason = video.FailureReason,
                UploadedBy = Labels.FullName(video.UploadedBy!),
                CreatedAt = video.CreatedAt
            };
        }
    }
}
