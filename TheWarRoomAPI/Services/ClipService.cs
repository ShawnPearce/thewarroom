using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class ClipService : IClipService
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public ClipService(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        public async Task<List<ClipDto>> GetClipsAsync(CurrentUser currentUser, ClipFilterDto filter)
        {
            FilmRules.EnsureCoach(currentUser);

            var query = ClipsOfTeam(currentUser.TeamId);

            if (filter.GameId != null)
            {
                query = query.Where(c => c.Video!.GameId == filter.GameId);
            }
            if (filter.VideoId != null)
            {
                query = query.Where(c => c.VideoId == filter.VideoId);
            }
            if (filter.GameIds.Count > 0)
            {
                query = query.Where(c => filter.GameIds.Contains(c.Video!.GameId));
            }
            if (filter.Tagged != null)
            {
                query = filter.Tagged.Value ? query.Where(c => c.Tag != null) : query.Where(c => c.Tag == null);
            }

            // The filters below look at the tag, so a play without a tag never matches them
            if (filter.OffenceTeamId != null)
            {
                query = query.Where(c => c.Tag!.OffenceTeamId == filter.OffenceTeamId);
            }
            if (filter.PlayType != null)
            {
                query = query.Where(c => c.Tag!.PlayType == filter.PlayType);
            }
            if (filter.Down != null)
            {
                query = query.Where(c => c.Tag!.Down == filter.Down);
            }
            if (filter.MinDistance != null)
            {
                query = query.Where(c => c.Tag!.Distance >= filter.MinDistance);
            }
            if (filter.MaxDistance != null)
            {
                query = query.Where(c => c.Tag!.Distance <= filter.MaxDistance);
            }
            if (filter.Distance != null)
            {
                var (firstYard, lastYard) = TendencyCalculator.Bounds(filter.Distance.Value);
                query = query.Where(c => c.Tag!.Distance >= firstYard && (lastYard == null || c.Tag!.Distance <= lastYard));
            }

            var clips = await query
                .OrderBy(c => c.Video!.Game!.GameDate)
                .ThenBy(c => c.VideoId)
                .ThenBy(c => c.StartMs)
                .ToListAsync();

            return clips.Select(FilmMapping.ToDto).ToList();
        }

        public async Task<ClipDto?> GetClipAsync(CurrentUser currentUser, int id)
        {
            FilmRules.EnsureCoach(currentUser);

            var clip = await ClipsOfTeam(currentUser.TeamId).FirstOrDefaultAsync(c => c.Id == id);
            if (clip == null)
            {
                return null;
            }

            return FilmMapping.ToDto(clip);
        }

        public async Task<ClipDto?> CreateAsync(CurrentUser currentUser, SaveClipDto dto)
        {
            FilmRules.EnsureCoach(currentUser);

            var video = await _context.Videos
                .Include(v => v.Game)
                .FirstOrDefaultAsync(v => v.Id == dto.VideoId && v.TeamId == currentUser.TeamId);
            if (video == null)
            {
                return null;
            }

            EnsureTimesFit(dto.StartMs, dto.EndMs, video);
            if (dto.Tag != null)
            {
                EnsureTagIsValid(dto.Tag, video.Game!);
            }

            var now = _clock.GetUtcNow().UtcDateTime;

            // Map DTO → entity (the server sets the team and who cut the clip)
            var clip = new Clip
            {
                TeamId = currentUser.TeamId,
                VideoId = video.Id,
                StartMs = dto.StartMs,
                EndMs = dto.EndMs,
                Title = Clean(dto.Title),
                CreatedById = currentUser.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            if (dto.Tag != null)
            {
                clip.Tag = NewTag(currentUser.TeamId, dto.Tag);
            }

            _context.Clips.Add(clip);
            await _context.SaveChangesAsync();

            return await GetClipAsync(currentUser, clip.Id);
        }

        public async Task<bool> UpdateAsync(CurrentUser currentUser, int id, SaveClipDto dto)
        {
            FilmRules.EnsureCoach(currentUser);

            var clip = await _context.Clips
                .Include(c => c.Video!)
                    .ThenInclude(v => v.Game)
                .Include(c => c.Tag)
                .FirstOrDefaultAsync(c => c.Id == id && c.TeamId == currentUser.TeamId);
            if (clip == null)
            {
                return false;
            }

            EnsureTimesFit(dto.StartMs, dto.EndMs, clip.Video!);
            if (dto.Tag != null)
            {
                EnsureTagIsValid(dto.Tag, clip.Video!.Game!);
            }

            clip.StartMs = dto.StartMs;
            clip.EndMs = dto.EndMs;
            clip.Title = Clean(dto.Title);
            clip.UpdatedAt = _clock.GetUtcNow().UtcDateTime;

            // The tag is added, changed or taken away
            if (dto.Tag == null)
            {
                if (clip.Tag != null)
                {
                    _context.PlayTags.Remove(clip.Tag);
                }
            }
            else if (clip.Tag == null)
            {
                clip.Tag = NewTag(currentUser.TeamId, dto.Tag);
            }
            else
            {
                clip.Tag.PlayType = dto.Tag.PlayType;
                clip.Tag.Down = dto.Tag.Down;
                clip.Tag.Distance = dto.Tag.Distance;
                clip.Tag.OffenceTeamId = dto.Tag.OffenceTeamId;
            }

            // Cutups that are still drafts follow the clip.
            // A cutup that has been made into a video keeps its own copy, so film that was shared does not change.
            var draftPlays = await _context.CutupClips
                .Where(p => p.ClipId == clip.Id && p.Cutup!.Status == CutupStatus.Draft)
                .ToListAsync();
            foreach (var play in draftPlays)
            {
                play.StartMs = clip.StartMs;
                play.EndMs = clip.EndMs;
                play.Title = clip.Title;
                play.AnnotationsJson = clip.AnnotationsJson;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(CurrentUser currentUser, int id)
        {
            FilmRules.EnsureCoach(currentUser);

            var clip = await _context.Clips.FirstOrDefaultAsync(c => c.Id == id && c.TeamId == currentUser.TeamId);
            if (clip == null)
            {
                return false;
            }

            // Business rule: a clip that is used in a cutup cannot be deleted
            var usedIn = await _context.CutupClips
                .Where(p => p.ClipId == clip.Id)
                .Select(p => p.Cutup!.Name)
                .FirstOrDefaultAsync();
            if (usedIn != null)
            {
                throw new InvalidOperationException($"This play is used in the cutup \"{usedIn}\". Take it out of the cutup first.");
            }

            _context.Clips.Remove(clip);   // its tag is deleted with it
            await _context.SaveChangesAsync();
            return true;
        }

        // ---------- Helpers ----------

        // Everything FilmMapping.ToDto needs
        private IQueryable<Clip> ClipsOfTeam(int teamId)
        {
            return _context.Clips
                .Where(c => c.TeamId == teamId)
                .Include(c => c.Video!)
                    .ThenInclude(v => v.Game!)
                        .ThenInclude(g => g.HomeTeam)
                .Include(c => c.Video!)
                    .ThenInclude(v => v.Game!)
                        .ThenInclude(g => g.AwayTeam)
                .Include(c => c.Tag!)
                    .ThenInclude(t => t.OffenceTeam)
                .Include(c => c.CutupClips)
                .AsSplitQuery();
        }

        // Business rule: a clip lies inside its video, and its end comes after its start.
        // The video must be ready, because only then is its length known.
        private static void EnsureTimesFit(int startMs, int endMs, Video video)
        {
            if (video.Status != VideoStatus.Ready || video.DurationMs == null)
            {
                throw new InvalidOperationException("This video is not ready yet, so it cannot be cut into plays.");
            }

            if (startMs < 0)
            {
                throw new InvalidOperationException("A clip cannot start before the beginning of the video.");
            }

            if (endMs <= startMs)
            {
                throw new InvalidOperationException("The end of a clip must come after its start.");
            }

            if (endMs > video.DurationMs)
            {
                throw new InvalidOperationException(
                    $"The clip ends after the end of the video, which is {Labels.Time(video.DurationMs.Value)} long.");
            }
        }

        private static void EnsureTagIsValid(SavePlayTagDto tag, Game game)
        {
            if (tag.PlayType != PlayType.Run && tag.PlayType != PlayType.Pass)
            {
                throw new InvalidOperationException("A tagged play is a run or a pass.");
            }

            if (tag.Down < 1 || tag.Down > 4)
            {
                throw new InvalidOperationException("The down must be between 1 and 4.");
            }

            if (tag.Distance < 1 || tag.Distance > 99)
            {
                throw new InvalidOperationException("The distance must be between 1 and 99 yards.");
            }

            // Business rule: the team on offence is one of the two teams that played the game
            if (tag.OffenceTeamId != game.HomeTeamId && tag.OffenceTeamId != game.AwayTeamId)
            {
                throw new InvalidOperationException("The team on offence must be one of the two teams in this game.");
            }
        }

        private static PlayTag NewTag(int teamId, SavePlayTagDto dto)
        {
            return new PlayTag
            {
                TeamId = teamId,
                PlayType = dto.PlayType,
                Down = dto.Down,
                Distance = dto.Distance,
                OffenceTeamId = dto.OffenceTeamId
            };
        }

        private static string? Clean(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
