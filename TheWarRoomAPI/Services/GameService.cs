using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class GameService : IGameService
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public GameService(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        public async Task<List<GameDto>> GetGamesAsync(CurrentUser currentUser, int? scoutedTeamId)
        {
            FilmRules.EnsureCoach(currentUser);

            var query = _context.Games.Where(g => g.TeamId == currentUser.TeamId);
            if (scoutedTeamId != null)
            {
                query = query.Where(g => g.HomeTeamId == scoutedTeamId || g.AwayTeamId == scoutedTeamId);
            }

            return await query
                .OrderByDescending(g => g.GameDate)
                .ThenByDescending(g => g.Id)
                .Select(g => new GameDto
                {
                    Id = g.Id,
                    GameDate = g.GameDate,
                    HomeTeamId = g.HomeTeamId,
                    HomeTeamName = g.HomeTeam!.Name,
                    AwayTeamId = g.AwayTeamId,
                    AwayTeamName = g.AwayTeam!.Name,
                    Label = g.Label,
                    Notes = g.Notes,
                    VideoCount = g.Videos!.Count,
                    ClipCount = g.Videos!.SelectMany(v => v.Clips!).Count(),
                    TaggedClipCount = g.Videos!.SelectMany(v => v.Clips!).Count(c => c.Tag != null)
                })
                .ToListAsync();
        }

        public async Task<GameDetailDto?> GetGameAsync(CurrentUser currentUser, int id)
        {
            FilmRules.EnsureCoach(currentUser);

            var game = await _context.Games
                .Where(g => g.Id == id && g.TeamId == currentUser.TeamId)
                .Select(g => new GameDetailDto
                {
                    Id = g.Id,
                    GameDate = g.GameDate,
                    HomeTeamId = g.HomeTeamId,
                    HomeTeamName = g.HomeTeam!.Name,
                    AwayTeamId = g.AwayTeamId,
                    AwayTeamName = g.AwayTeam!.Name,
                    Label = g.Label,
                    Notes = g.Notes,
                    VideoCount = g.Videos!.Count,
                    ClipCount = g.Videos!.SelectMany(v => v.Clips!).Count(),
                    TaggedClipCount = g.Videos!.SelectMany(v => v.Clips!).Count(c => c.Tag != null)
                })
                .FirstOrDefaultAsync();
            if (game == null)
            {
                return null;
            }

            game.Videos = await _context.Videos
                .Where(v => v.GameId == game.Id)
                .OrderBy(v => v.Id)
                .Select(v => new VideoDto
                {
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
                    CreatedAt = v.CreatedAt
                })
                .ToListAsync();

            return game;
        }

        public async Task<GameDto> CreateAsync(CurrentUser currentUser, SaveGameDto dto)
        {
            FilmRules.EnsureCoach(currentUser);
            await EnsureTeamsAreValidAsync(currentUser, dto);

            // Map DTO → entity (the server sets the team and who added it)
            var game = new Game
            {
                TeamId = currentUser.TeamId,
                GameDate = dto.GameDate,
                HomeTeamId = dto.HomeTeamId,
                AwayTeamId = dto.AwayTeamId,
                Label = Clean(dto.Label),
                Notes = Clean(dto.Notes),
                CreatedById = currentUser.Id,
                CreatedAt = _clock.GetUtcNow().UtcDateTime
            };

            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            return (await GetGameAsync(currentUser, game.Id))!;
        }

        public async Task<bool> UpdateAsync(CurrentUser currentUser, int id, SaveGameDto dto)
        {
            FilmRules.EnsureCoach(currentUser);

            var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == id && g.TeamId == currentUser.TeamId);
            if (game == null)
            {
                return false;
            }

            await EnsureTeamsAreValidAsync(currentUser, dto);

            // Business rule: a play is tagged with the team on offence, so that team has to stay in the game
            var teamStillNeeded = await _context.PlayTags
                .Where(t => t.Clip!.Video!.GameId == game.Id
                    && t.OffenceTeamId != dto.HomeTeamId
                    && t.OffenceTeamId != dto.AwayTeamId)
                .Select(t => t.OffenceTeam!.Name)
                .FirstOrDefaultAsync();
            if (teamStillNeeded != null)
            {
                throw new InvalidOperationException(
                    $"Plays in this game are tagged with {teamStillNeeded} on offence, so {teamStillNeeded} has to stay one of its two teams.");
            }

            game.GameDate = dto.GameDate;
            game.HomeTeamId = dto.HomeTeamId;
            game.AwayTeamId = dto.AwayTeamId;
            game.Label = Clean(dto.Label);
            game.Notes = Clean(dto.Notes);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(CurrentUser currentUser, int id)
        {
            FilmRules.EnsureCoach(currentUser);

            var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == id && g.TeamId == currentUser.TeamId);
            if (game == null)
            {
                return false;
            }

            // Business rule: a game is removed only when it has no video, so film is never deleted by accident
            bool hasVideo = await _context.Videos.AnyAsync(v => v.GameId == game.Id);
            if (hasVideo)
            {
                throw new InvalidOperationException("This game has video. Delete its video first.");
            }

            _context.Games.Remove(game);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task EnsureTeamsAreValidAsync(CurrentUser currentUser, SaveGameDto dto)
        {
            // Business rule: a game is played between two different teams
            if (dto.HomeTeamId == dto.AwayTeamId)
            {
                throw new InvalidOperationException("A game needs two different teams.");
            }

            // Business rule: both are teams on this team's film
            int teamsFound = await _context.ScoutedTeams.CountAsync(t =>
                t.TeamId == currentUser.TeamId && (t.Id == dto.HomeTeamId || t.Id == dto.AwayTeamId));
            if (teamsFound != 2)
            {
                throw new InvalidOperationException("Choose both teams from the teams on film.");
            }
        }

        private static string? Clean(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
