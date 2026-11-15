using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class CutupService : ICutupService
    {
        // A cutup is one video in the end, so it is kept to a length that can be made and watched
        public const int MaxPlays = 200;

        // A signed link to a finished cutup lasts a long film session
        private static readonly TimeSpan PlaybackLinkLife = TimeSpan.FromHours(6);

        private readonly AppDbContext _context;
        private readonly IFilmStorage _storage;
        private readonly IVideoTools _tools;
        private readonly TimeProvider _clock;

        public CutupService(AppDbContext context, IFilmStorage storage, IVideoTools tools, TimeProvider clock)
        {
            _context = context;
            _storage = storage;
            _tools = tools;
            _clock = clock;
        }

        // ---------- Reading ----------

        public async Task<List<CutupDto>> GetCutupsAsync(CurrentUser currentUser)
        {
            var cutups = await CutupsOfTeam(currentUser.TeamId).ToListAsync();

            // Permission: a coach sees every cutup of the team; a player only what was shared with them
            return cutups
                .Where(c => AccessRules.CanWatchCutup(currentUser, c.Assignments!))
                .OrderByDescending(c => c.ForWeekOf ?? DateOnly.FromDateTime(c.CreatedAt))   // the latest week first
                .ThenByDescending(c => c.Id)
                .Select(c => ToDto(new CutupDto(), c, currentUser))
                .ToList();
        }

        public async Task<CutupDetailDto?> GetCutupAsync(CurrentUser currentUser, int id)
        {
            var cutup = await CutupsOfTeam(currentUser.TeamId)
                .Include(c => c.Clips!)
                    .ThenInclude(p => p.Video!)
                        .ThenInclude(v => v.Game!)
                            .ThenInclude(g => g.HomeTeam)
                .Include(c => c.Clips!)
                    .ThenInclude(p => p.Video!)
                        .ThenInclude(v => v.Game!)
                            .ThenInclude(g => g.AwayTeam)
                .Include(c => c.Clips!)
                    .ThenInclude(p => p.Clip!)
                        .ThenInclude(clip => clip.Tag!)
                            .ThenInclude(t => t.OffenceTeam)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (cutup == null)
            {
                return null;
            }

            // Business rule: a player reaches only film shared with them, with one of their groups, or with the team
            if (!AccessRules.CanWatchCutup(currentUser, cutup.Assignments!))
            {
                throw new ForbiddenException("This film has not been shared with you.");
            }

            var detail = (CutupDetailDto)ToDto(new CutupDetailDto(), cutup, currentUser);

            // Each play starts where the one before it ends.
            // Once the video has been made, where each play is in it was measured; before that it is added up.
            var plays = cutup.Clips!.OrderBy(p => p.Position).ToList();
            var offsets = CutupTimeline.Offsets(plays.Select(p => p.EndMs - p.StartMs));
            bool measured = cutup.Status == CutupStatus.Ready && plays.All(p => p.OutputOffsetMs != null && p.OutputDurationMs != null);

            detail.Plays = plays
                .Select((play, index) => new CutupPlayDto
                {
                    Id = play.Id,
                    Position = play.Position,
                    Title = play.Title,
                    GameDate = play.Video!.Game!.GameDate,
                    Matchup = Labels.Matchup(play.Video.Game),
                    StartMs = play.StartMs,
                    EndMs = play.EndMs,
                    OffsetMs = measured ? play.OutputOffsetMs!.Value : offsets[index],
                    DurationMs = measured ? play.OutputDurationMs!.Value : play.EndMs - play.StartMs,
                    AnnotationsJson = play.AnnotationsJson,

                    // The clip and its tag are the coaches' analysis; players get the film itself
                    ClipId = currentUser.IsCoach ? play.ClipId : null,
                    Tag = currentUser.IsCoach ? FilmMapping.ToDto(play.Clip!.Tag) : null
                })
                .ToList();

            if (currentUser.IsCoach && (cutup.Status == CutupStatus.Draft || cutup.Status == CutupStatus.Failed))
            {
                detail.RenderProblem = WhyNoVideoCanBeMade(cutup);
            }

            if (currentUser.IsCoach)
            {
                detail.Sharing = new CutupSharingDto
                {
                    WholeTeam = cutup.Assignments!.Any(a => a.Audience == FilmAudience.Team),
                    Groups = cutup.Assignments!
                        .Where(a => a.Audience == FilmAudience.Group)
                        .OrderBy(a => a.PositionGroup!.SortOrder)
                        .Select(a => new GroupSummaryDto { Id = a.PositionGroup!.Id, Name = a.PositionGroup.Name, Unit = a.PositionGroup.Unit })
                        .ToList(),
                    Players = cutup.Assignments!
                        .Where(a => a.Audience == FilmAudience.Player)
                        .OrderBy(a => a.Player!.LastName)
                        .ThenBy(a => a.Player!.FirstName)
                        .Select(a => new PersonDto
                        {
                            Id = a.Player!.Id,
                            FirstName = a.Player.FirstName,
                            LastName = a.Player.LastName,
                            JerseyNumber = a.Player.RosterEntry?.JerseyNumber
                        })
                        .ToList()
                };
            }

            return detail;
        }

        // ---------- Creating and changing ----------

        public async Task<CutupDto> CreateAsync(CurrentUser currentUser, SaveCutupDto dto)
        {
            FilmRules.EnsureCoach(currentUser);

            var now = _clock.GetUtcNow().UtcDateTime;

            // Map DTO → entity (the server sets the team and who made it). A new cutup is a draft with no plays.
            var cutup = new Cutup
            {
                TeamId = currentUser.TeamId,
                Name = dto.Name.Trim(),
                Description = Clean(dto.Description),
                ForWeekOf = WeekOf(dto.ForWeekOf),
                Status = CutupStatus.Draft,
                CreatedById = currentUser.Id,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Cutups.Add(cutup);
            await _context.SaveChangesAsync();

            return (await GetCutupAsync(currentUser, cutup.Id))!;
        }

        public async Task<bool> UpdateAsync(CurrentUser currentUser, int id, SaveCutupDto dto)
        {
            FilmRules.EnsureCoach(currentUser);

            var cutup = await _context.Cutups.FirstOrDefaultAsync(c => c.Id == id && c.TeamId == currentUser.TeamId);
            if (cutup == null)
            {
                return false;
            }

            // The name, description and week describe the film; they can be changed at any time
            cutup.Name = dto.Name.Trim();
            cutup.Description = Clean(dto.Description);
            cutup.ForWeekOf = WeekOf(dto.ForWeekOf);
            cutup.UpdatedAt = _clock.GetUtcNow().UtcDateTime;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(CurrentUser currentUser, int id)
        {
            FilmRules.EnsureCoach(currentUser);

            var cutup = await _context.Cutups.FirstOrDefaultAsync(c => c.Id == id && c.TeamId == currentUser.TeamId);
            if (cutup == null)
            {
                return false;
            }

            if (cutup.Status == CutupStatus.Rendering)
            {
                throw new InvalidOperationException("This cutup is being made into a video right now. Delete it when that has finished.");
            }

            string? key = cutup.StorageKey;

            _context.Cutups.Remove(cutup);   // its plays and its sharing are deleted with it; the clips stay
            await _context.SaveChangesAsync();

            // The video that was made goes after the record, so a record never points at a file that is gone
            if (key != null)
            {
                await _storage.DeleteAsync(key, CancellationToken.None);
            }

            return true;
        }

        // ---------- The plays of a cutup ----------

        public async Task<bool> AddClipsAsync(CurrentUser currentUser, int id, AddCutupClipsDto dto)
        {
            FilmRules.EnsureCoach(currentUser);

            var cutup = await _context.Cutups
                .Include(c => c.Clips)
                .FirstOrDefaultAsync(c => c.Id == id && c.TeamId == currentUser.TeamId);
            if (cutup == null)
            {
                return false;
            }

            EnsureIsDraft(cutup);

            // Business rule: only plays from this team's film can be put in its cutups
            var clipIds = dto.ClipIds.Distinct().ToList();
            var clips = await _context.Clips
                .Where(c => c.TeamId == currentUser.TeamId && clipIds.Contains(c.Id))
                .ToListAsync();
            if (clips.Count != clipIds.Count)
            {
                throw new InvalidOperationException("One of the chosen plays is not in this team's film.");
            }

            // New plays go to the end, in the order they were chosen.
            // A play that is already in the cutup stays where it is.
            var plays = cutup.Clips!;
            int position = plays.Count;
            foreach (int clipId in clipIds)
            {
                if (plays.Any(p => p.ClipId == clipId))
                {
                    continue;
                }

                var clip = clips.First(c => c.Id == clipId);
                plays.Add(new CutupClip
                {
                    TeamId = currentUser.TeamId,
                    Position = ++position,
                    ClipId = clip.Id,

                    // The copy of the clip (see CutupClip)
                    VideoId = clip.VideoId,
                    StartMs = clip.StartMs,
                    EndMs = clip.EndMs,
                    Title = clip.Title,
                    AnnotationsJson = clip.AnnotationsJson
                });
            }

            if (plays.Count > MaxPlays)
            {
                throw new InvalidOperationException($"A cutup can have at most {MaxPlays} plays.");
            }

            cutup.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MovePlayAsync(CurrentUser currentUser, int playId, MoveDirection direction)
        {
            FilmRules.EnsureCoach(currentUser);

            var cutup = await FindCutupOfPlayAsync(currentUser, playId);
            if (cutup == null)
            {
                return false;
            }

            EnsureIsDraft(cutup);

            // Swap the play with its neighbour. Moving the first play up, or the last one down, changes nothing.
            var plays = cutup.Clips!.OrderBy(p => p.Position).ToList();
            int index = plays.FindIndex(p => p.Id == playId);
            int neighbour = direction == MoveDirection.Up ? index - 1 : index + 1;
            if (neighbour >= 0 && neighbour < plays.Count)
            {
                (plays[index], plays[neighbour]) = (plays[neighbour], plays[index]);
            }

            Renumber(plays);
            cutup.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemovePlayAsync(CurrentUser currentUser, int playId)
        {
            FilmRules.EnsureCoach(currentUser);

            var cutup = await FindCutupOfPlayAsync(currentUser, playId);
            if (cutup == null)
            {
                return false;
            }

            EnsureIsDraft(cutup);

            var plays = cutup.Clips!.OrderBy(p => p.Position).ToList();
            var play = plays.First(p => p.Id == playId);
            plays.Remove(play);
            _context.CutupClips.Remove(play);

            // The plays after it move up, so the order stays 1, 2, 3...
            Renumber(plays);
            cutup.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
            await _context.SaveChangesAsync();
            return true;
        }

        // ---------- Sharing ----------

        public async Task<bool> SetSharingAsync(CurrentUser currentUser, int id, SetCutupSharingDto dto)
        {
            FilmRules.EnsureCoach(currentUser);

            var cutup = await _context.Cutups
                .Include(c => c.Assignments)
                .FirstOrDefaultAsync(c => c.Id == id && c.TeamId == currentUser.TeamId);
            if (cutup == null)
            {
                return false;
            }

            var groupIds = dto.GroupIds.Distinct().ToList();
            var playerIds = dto.PlayerIds.Distinct().ToList();

            // Business rule: film is shared inside the team only, with its groups and its active players
            int groupsFound = await _context.PositionGroups.CountAsync(g => g.TeamId == currentUser.TeamId && groupIds.Contains(g.Id));
            if (groupsFound != groupIds.Count)
            {
                throw new InvalidOperationException("One of the chosen groups is not on this team.");
            }

            int playersFound = await _context.Users.CountAsync(u =>
                u.TeamId == currentUser.TeamId && u.Role == UserRole.Player && u.IsActive && playerIds.Contains(u.Id));
            if (playersFound != playerIds.Count)
            {
                throw new InvalidOperationException("One of the chosen people is not an active player on this team.");
            }

            // Take away what is no longer chosen, then add what is new.
            // What stays keeps the record of who shared it and when.
            var assignments = cutup.Assignments!;
            assignments.RemoveAll(a =>
                (a.Audience == FilmAudience.Team && !dto.WholeTeam)
                || (a.Audience == FilmAudience.Group && !groupIds.Contains(a.PositionGroupId!.Value))
                || (a.Audience == FilmAudience.Player && !playerIds.Contains(a.PlayerId!.Value)));

            var now = _clock.GetUtcNow().UtcDateTime;

            if (dto.WholeTeam && !assignments.Any(a => a.Audience == FilmAudience.Team))
            {
                assignments.Add(new FilmAssignment { Audience = FilmAudience.Team, AssignedById = currentUser.Id, AssignedAt = now });
            }

            foreach (int groupId in groupIds)
            {
                if (!assignments.Any(a => a.Audience == FilmAudience.Group && a.PositionGroupId == groupId))
                {
                    assignments.Add(new FilmAssignment
                    {
                        Audience = FilmAudience.Group,
                        PositionGroupId = groupId,
                        AssignedById = currentUser.Id,
                        AssignedAt = now
                    });
                }
            }

            foreach (int playerId in playerIds)
            {
                if (!assignments.Any(a => a.Audience == FilmAudience.Player && a.PlayerId == playerId))
                {
                    assignments.Add(new FilmAssignment
                    {
                        Audience = FilmAudience.Player,
                        PlayerId = playerId,
                        AssignedById = currentUser.Id,
                        AssignedAt = now
                    });
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // ---------- The video of a cutup ----------

        public async Task<bool> RequestRenderAsync(CurrentUser currentUser, int id)
        {
            FilmRules.EnsureCoach(currentUser);

            var cutup = await CutupWithItsVideosAsync(currentUser, id);
            if (cutup == null)
            {
                return false;
            }

            // A draft is made into a video for the first time; one that failed can be tried again
            if (cutup.Status == CutupStatus.Ready)
            {
                throw new InvalidOperationException("This cutup has already been made into a video.");
            }
            if (cutup.Status == CutupStatus.Queued || cutup.Status == CutupStatus.Rendering)
            {
                throw new InvalidOperationException("This cutup is already being made into a video.");
            }

            string? problem = WhyNoVideoCanBeMade(cutup);
            if (problem != null)
            {
                throw new InvalidOperationException(problem);
            }

            // Business rule: from here on the plays are fixed. The row itself is the place in the queue:
            // the background worker makes the cutup that has waited longest (see CutupRenderer).
            var now = _clock.GetUtcNow().UtcDateTime;
            cutup.Status = CutupStatus.Queued;
            cutup.QueuedAt = now;
            cutup.ProgressPercent = 0;
            cutup.FailureReason = null;
            cutup.UpdatedAt = now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReturnToDraftAsync(CurrentUser currentUser, int id)
        {
            FilmRules.EnsureCoach(currentUser);

            var cutup = await _context.Cutups
                .Include(c => c.Clips!)
                    .ThenInclude(p => p.Clip)
                .FirstOrDefaultAsync(c => c.Id == id && c.TeamId == currentUser.TeamId);
            if (cutup == null)
            {
                return false;
            }

            if (cutup.Status == CutupStatus.Draft)
            {
                return true;   // it already is one
            }
            if (cutup.Status == CutupStatus.Rendering)
            {
                throw new InvalidOperationException("This cutup is being made into a video right now. Try again when that has finished.");
            }

            string? key = cutup.StorageKey;

            cutup.Status = CutupStatus.Draft;
            cutup.StorageKey = null;
            cutup.DurationMs = null;
            cutup.ProgressPercent = 0;
            cutup.QueuedAt = null;
            cutup.RenderedAt = null;
            cutup.FailureReason = null;
            cutup.UpdatedAt = _clock.GetUtcNow().UtcDateTime;

            // A draft follows its clips again, so it catches up with what was changed on them in the meantime
            foreach (var play in cutup.Clips!)
            {
                play.OutputOffsetMs = null;
                play.OutputDurationMs = null;
                play.StartMs = play.Clip!.StartMs;
                play.EndMs = play.Clip.EndMs;
                play.Title = play.Clip.Title;
                play.AnnotationsJson = play.Clip.AnnotationsJson;
            }

            await _context.SaveChangesAsync();

            if (key != null)
            {
                await _storage.DeleteAsync(key, CancellationToken.None);
            }

            return true;
        }

        public async Task<FilmFile?> GetFileAsync(CurrentUser currentUser, int id)
        {
            var cutup = await _context.Cutups
                .Include(c => c.Assignments)
                .FirstOrDefaultAsync(c => c.Id == id && c.TeamId == currentUser.TeamId);
            if (cutup == null)
            {
                return null;
            }

            // Business rule: a player reaches only film shared with them, with one of their groups, or with the team
            if (!AccessRules.CanWatchCutup(currentUser, cutup.Assignments!))
            {
                throw new ForbiddenException("This film has not been shared with you.");
            }

            if (cutup.Status != CutupStatus.Ready || cutup.StorageKey == null)
            {
                return null;
            }

            if (_storage.UsesSignedLinks)
            {
                return new FilmFile { SignedLink = _storage.CreatePlaybackLink(cutup.StorageKey, PlaybackLinkLife) };
            }

            string? path = _storage.FindFile(cutup.StorageKey);
            return path == null ? null : new FilmFile { Path = path };
        }

        // ---------- Helpers ----------

        // A cutup with its plays and the video each play is cut from
        private async Task<Cutup?> CutupWithItsVideosAsync(CurrentUser currentUser, int id)
        {
            return await _context.Cutups
                .Include(c => c.Clips!)
                    .ThenInclude(p => p.Video)
                .FirstOrDefaultAsync(c => c.Id == id && c.TeamId == currentUser.TeamId);
        }

        // Empty when the cutup can be made into a video; otherwise the reason it cannot, written for the coach.
        // The cutup must be loaded with its plays and their videos.
        private string? WhyNoVideoCanBeMade(Cutup cutup)
        {
            if (cutup.Clips!.Count == 0)
            {
                return "Add plays to the cutup first.";
            }

            // Business rule: a video is made of film that is in storage.
            // A play from a record without a file cannot be part of it.
            var withoutFile = cutup.Clips
                .OrderBy(p => p.Position)
                .FirstOrDefault(p => p.Video!.Status != VideoStatus.Ready || p.Video.StorageKey == null);
            if (withoutFile != null)
            {
                return $"Play {withoutFile.Position} is cut from \"{withoutFile.Video!.Title}\", a video without a file. " +
                       "Take that play out, or upload the video and cut the play from it.";
            }

            return _storage.Problem ?? _tools.Problem;
        }

        // A team's cutups with what the list needs: who made each, its plays, and who it is shared with
        private IQueryable<Cutup> CutupsOfTeam(int teamId)
        {
            return _context.Cutups
                .Where(c => c.TeamId == teamId)
                .Include(c => c.CreatedBy)
                .Include(c => c.Clips)
                .Include(c => c.Assignments!)
                    .ThenInclude(a => a.PositionGroup)
                .Include(c => c.Assignments!)
                    .ThenInclude(a => a.Player!)
                        .ThenInclude(p => p.RosterEntry)
                .AsSplitQuery();
        }

        // The cutup a play belongs to, with all its plays; null when the play is not on the current user's team
        private async Task<Cutup?> FindCutupOfPlayAsync(CurrentUser currentUser, int playId)
        {
            return await _context.Cutups
                .Include(c => c.Clips)
                .FirstOrDefaultAsync(c => c.TeamId == currentUser.TeamId && c.Clips!.Any(p => p.Id == playId));
        }

        // Business rule: the plays of a cutup can be changed only while it is a draft.
        // Once it is being made into a video, or has been, the film stays as it is.
        private static void EnsureIsDraft(Cutup cutup)
        {
            if (cutup.Status != CutupStatus.Draft)
            {
                throw new InvalidOperationException(
                    "The plays of this cutup are fixed, because it is being made or has been made into a video. " +
                    "Turn it back into a draft to change them.");
            }
        }

        // Positions are stored as 1, 2, 3... with no gaps
        private static void Renumber(List<CutupClip> plays)
        {
            for (int i = 0; i < plays.Count; i++)
            {
                plays[i].Position = i + 1;
            }
        }

        // A week is identified by its Monday, the same way the depth chart does it
        private static DateOnly? WeekOf(DateOnly? date)
        {
            return date == null ? null : WeekDates.MondayOf(date.Value);
        }

        private static string? Clean(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        // Map entity → DTO (works for the list row and for the detail, which adds to it)
        private static CutupDto ToDto(CutupDto dto, Cutup cutup, CurrentUser currentUser)
        {
            dto.Id = cutup.Id;
            dto.Name = cutup.Name;
            dto.Description = cutup.Description;
            dto.ForWeekOf = cutup.ForWeekOf;
            dto.Status = cutup.Status;
            dto.HasVideo = cutup.Status == CutupStatus.Ready && cutup.StorageKey != null;
            dto.ProgressPercent = cutup.ProgressPercent;
            dto.PlayCount = cutup.Clips!.Count;
            dto.DurationMs = dto.HasVideo && cutup.DurationMs != null
                ? cutup.DurationMs.Value
                : cutup.Clips.Sum(p => p.EndMs - p.StartMs);
            dto.CreatedBy = Labels.FullName(cutup.CreatedBy!);
            dto.CreatedAt = cutup.CreatedAt;
            dto.CanManage = AccessRules.CanManageFilm(currentUser);

            // Who else it is shared with, and why a video could not be made, are the coaches' business
            if (currentUser.IsCoach)
            {
                dto.SharedWith = SharingInWords(cutup);
                dto.FailureReason = cutup.Status == CutupStatus.Failed ? cutup.FailureReason : null;
            }

            return dto;
        }

        // "Whole team", then the groups, then the players
        private static List<string> SharingInWords(Cutup cutup)
        {
            var words = new List<string>();
            var assignments = cutup.Assignments!;

            if (assignments.Any(a => a.Audience == FilmAudience.Team))
            {
                words.Add("Whole team");
            }

            words.AddRange(assignments
                .Where(a => a.Audience == FilmAudience.Group)
                .OrderBy(a => a.PositionGroup!.SortOrder)
                .Select(a => a.PositionGroup!.Name));

            words.AddRange(assignments
                .Where(a => a.Audience == FilmAudience.Player)
                .OrderBy(a => a.Player!.LastName)
                .ThenBy(a => a.Player!.FirstName)
                .Select(a => a.Player!.RosterEntry?.JerseyNumber != null
                    ? $"#{a.Player.RosterEntry.JerseyNumber} {Labels.FullName(a.Player)}"
                    : Labels.FullName(a.Player)));

            return words;
        }
    }
}
