using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class ScoutedTeamService : IScoutedTeamService
    {
        private readonly AppDbContext _context;

        public ScoutedTeamService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ScoutedTeamDto>> GetTeamsAsync(CurrentUser currentUser)
        {
            FilmRules.EnsureCoach(currentUser);

            return await TeamsOf(currentUser.TeamId)
                .OrderByDescending(t => t.IsOwnTeam)   // the team itself first, then the opponents by name
                .ThenBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<ScoutedTeamDto?> GetTeamAsync(CurrentUser currentUser, int id)
        {
            FilmRules.EnsureCoach(currentUser);

            return await TeamsOf(currentUser.TeamId).FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<ScoutedTeamDto> CreateAsync(CurrentUser currentUser, SaveScoutedTeamDto dto)
        {
            FilmRules.EnsureCoach(currentUser);

            var name = dto.Name.Trim();
            await EnsureNameIsFreeAsync(currentUser.TeamId, name, 0);

            // Map DTO → entity (the server sets the team; a team added here is always an opponent)
            var team = new ScoutedTeam
            {
                TeamId = currentUser.TeamId,
                Name = name,
                IsOwnTeam = false
            };

            _context.ScoutedTeams.Add(team);
            await _context.SaveChangesAsync();

            return (await GetTeamAsync(currentUser, team.Id))!;
        }

        public async Task<bool> UpdateAsync(CurrentUser currentUser, int id, SaveScoutedTeamDto dto)
        {
            FilmRules.EnsureCoach(currentUser);

            var team = await _context.ScoutedTeams.FirstOrDefaultAsync(t => t.Id == id && t.TeamId == currentUser.TeamId);
            if (team == null)
            {
                return false;
            }

            // Business rule: the team's own entry always carries the team's name
            if (team.IsOwnTeam)
            {
                throw new InvalidOperationException("Your own team is renamed in the team settings. Its name on film follows.");
            }

            var name = dto.Name.Trim();
            await EnsureNameIsFreeAsync(currentUser.TeamId, name, team.Id);

            team.Name = name;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(CurrentUser currentUser, int id)
        {
            FilmRules.EnsureCoach(currentUser);

            var team = await _context.ScoutedTeams.FirstOrDefaultAsync(t => t.Id == id && t.TeamId == currentUser.TeamId);
            if (team == null)
            {
                return false;
            }

            if (team.IsOwnTeam)
            {
                throw new InvalidOperationException("Your own team cannot be removed from the teams on film.");
            }

            // Business rule: a team that played in a game on film stays until those games are deleted
            bool hasGames = await _context.Games.AnyAsync(g => g.HomeTeamId == team.Id || g.AwayTeamId == team.Id);
            if (hasGames)
            {
                throw new InvalidOperationException($"{team.Name} has games on film. Delete those games first.");
            }

            _context.ScoutedTeams.Remove(team);
            await _context.SaveChangesAsync();
            return true;
        }

        // Map entity → DTO, with the number of games the team played in
        private IQueryable<ScoutedTeamDto> TeamsOf(int teamId)
        {
            return _context.ScoutedTeams
                .Where(t => t.TeamId == teamId)
                .Select(t => new ScoutedTeamDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    IsOwnTeam = t.IsOwnTeam,
                    GameCount = _context.Games.Count(g => g.HomeTeamId == t.Id || g.AwayTeamId == t.Id)
                });
        }

        // Business rule: two teams on film cannot share a name (the comparison ignores upper/lower case)
        private async Task EnsureNameIsFreeAsync(int teamId, string name, int exceptId)
        {
            bool taken = await _context.ScoutedTeams.AnyAsync(t => t.TeamId == teamId && t.Name == name && t.Id != exceptId);
            if (taken)
            {
                throw new InvalidOperationException($"There is already a team called {name}.");
            }
        }
    }
}
