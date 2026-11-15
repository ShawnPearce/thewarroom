using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.Diagrams;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class TeamService : ITeamService
    {
        private readonly AppDbContext _context;
        private readonly IAuthService _authService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly TimeProvider _clock;
        private readonly SiteOptions _site;

        public TeamService(AppDbContext context, IAuthService authService, IPasswordHasher<User> passwordHasher, TimeProvider clock,
            IOptions<SiteOptions> site)
        {
            _context = context;
            _authService = authService;
            _passwordHasher = passwordHasher;
            _clock = clock;
            _site = site.Value;
        }

        public async Task<LoginResultDto> RegisterAsync(RegisterTeamDto dto)
        {
            // Business rule: new teams register only where the site allows it.
            // A public demo site has it switched off; accounts are then given out by head coaches only.
            if (!_site.AllowRegistration)
            {
                throw new ForbiddenException("New teams cannot be registered on this site. Ask your head coach for an account.");
            }

            var email = dto.Email.Trim();

            // Business rule: an email address can belong to only one account
            await AccountRules.EnsureEmailIsUniqueAsync(_context, email);

            var now = _clock.GetUtcNow().UtcDateTime;

            var team = new Team
            {
                Name = dto.TeamName.Trim(),
                AttendanceTrackingEnabled = false,
                Rules = dto.Rules ?? FootballRules.American,
                CreatedAt = now
            };

            // The person who registers the team becomes its head coach
            var headCoach = new User
            {
                Team = team,
                Email = email,
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Role = UserRole.HeadCoach,
                CanPublishPlaybook = true,
                CanEditDepthChart = true,
                CanRecordAttendance = true,
                MustChangePassword = false,
                IsActive = true,
                CreatedAt = now
            };
            headCoach.PasswordHash = _passwordHasher.HashPassword(headCoach, dto.Password);

            _context.Users.Add(headCoach);   // the team is saved with it through the navigation property

            // Every new team starts with the standard position groups, positions and playbooks of the game it plays
            TeamDefaults.AddTo(team, _context, now);

            await _context.SaveChangesAsync();

            return await _authService.StartSessionAsync(headCoach);
        }

        public async Task<TeamDto?> GetAsync(CurrentUser currentUser)
        {
            var team = await _context.Teams.FirstOrDefaultAsync(t => t.Id == currentUser.TeamId);
            if (team == null)
            {
                return null;
            }

            return new TeamDto
            {
                Id = team.Id,
                Name = team.Name,
                AttendanceTrackingEnabled = team.AttendanceTrackingEnabled,
                Rules = team.Rules,
                PlayersPerSide = DiagramValidator.PlayersPerSide(team.Rules),
                CreatedAt = team.CreatedAt
            };
        }

        public async Task<bool> UpdateAsync(CurrentUser currentUser, UpdateTeamDto dto)
        {
            if (!AccessRules.CanManageTeam(currentUser))
            {
                throw new ForbiddenException("Only the head coach can change the team's settings.");
            }

            var team = await _context.Teams.FirstOrDefaultAsync(t => t.Id == currentUser.TeamId);
            if (team == null)
            {
                return false;
            }

            var name = dto.Name.Trim();

            // The team's own entry among the teams on film carries the same name.
            // Business rule: two teams on film cannot share a name, so an opponent may not already have it.
            bool takenByOpponent = await _context.ScoutedTeams
                .AnyAsync(s => s.TeamId == team.Id && !s.IsOwnTeam && s.Name == name);
            if (takenByOpponent)
            {
                throw new InvalidOperationException($"An opponent on film is already called {name}. Rename or remove that opponent first.");
            }

            var ownTeamOnFilm = await _context.ScoutedTeams.FirstOrDefaultAsync(s => s.TeamId == team.Id && s.IsOwnTeam);
            if (ownTeamOnFilm != null)
            {
                ownTeamOnFilm.Name = name;
            }

            team.Name = name;
            team.AttendanceTrackingEnabled = dto.AttendanceTrackingEnabled;

            // Business rule: the head coach chooses the game the team plays, American (11 a side) or Canadian (12 a side).
            // From then on the team works with that game's positions and playbooks. The first time a team takes up
            // a game it is given the game's standard positions and base formations; what belongs only to the other
            // game is put away, not deleted, and is there again when the team goes back to it.
            if (dto.Rules != null && dto.Rules != team.Rules)
            {
                team.Rules = dto.Rules.Value;
                await TeamDefaults.AddWhatTheGameNeedsAsync(team, _context, _clock.GetUtcNow().UtcDateTime);
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
