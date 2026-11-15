using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.Diagrams;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class PlaybookService : IPlaybookService
    {
        private readonly AppDbContext _context;
        private readonly PlaybookChangeApplier _applier;

        public PlaybookService(AppDbContext context, PlaybookChangeApplier applier)
        {
            _context = context;
            _applier = applier;
        }

        // ---------- Reading (approved content only) ----------

        public async Task<List<PlaybookDto>> GetPlaybooksAsync(CurrentUser currentUser)
        {
            // Business rule: the team works in the three playbooks of the game it plays.
            // The playbooks of the other game are put away: kept as they were left, but not listed.
            var playbooks = await _context.Playbooks
                .Where(p => p.TeamId == currentUser.TeamId && p.Rules == currentUser.Rules)
                .OrderBy(p => p.Unit)
                .Select(p => new PlaybookDto
                {
                    Id = p.Id,
                    Unit = p.Unit,
                    Name = p.Name,
                    Rules = p.Rules,
                    FormationCount = p.Formations!.Count,
                    PlayCount = p.Formations!.SelectMany(f => f.Plays!).Count()
                })
                .ToListAsync();

            // A player gets the playbooks of the units their groups belong to; a coach gets all three
            playbooks = playbooks.Where(p => AccessRules.CanReadPlaybook(currentUser, p.Unit)).ToList();

            foreach (var playbook in playbooks)
            {
                playbook.PlayersPerSide = DiagramValidator.PlayersPerSide(playbook.Rules);
                playbook.CanChange = AccessRules.CanChangePlaybook(currentUser, playbook.Unit);
                playbook.CanPublish = AccessRules.CanPublishPlaybook(currentUser, playbook.Unit);
            }

            return playbooks;
        }

        public async Task<PlaybookDetailDto?> GetPlaybookAsync(CurrentUser currentUser, int id)
        {
            var playbook = await _context.Playbooks
                .Include(p => p.Formations!)
                    .ThenInclude(f => f.Plays)
                .FirstOrDefaultAsync(p => p.Id == id && p.TeamId == currentUser.TeamId);
            if (playbook == null)
            {
                return null;
            }

            EnsureCanRead(currentUser, playbook.Unit);

            var pendingFormationIds = await PendingFormationIdsAsync(currentUser, playbook.Id);
            bool putAway = IsPutAway(currentUser, playbook);

            return new PlaybookDetailDto
            {
                Id = playbook.Id,
                Unit = playbook.Unit,
                Name = playbook.Name,
                Rules = playbook.Rules,
                PlayersPerSide = DiagramValidator.PlayersPerSide(playbook.Rules),
                IsPutAway = putAway,
                FormationCount = playbook.Formations!.Count,
                PlayCount = playbook.Formations.Sum(f => f.Plays!.Count),
                CanChange = !putAway && AccessRules.CanChangePlaybook(currentUser, playbook.Unit),
                CanPublish = !putAway && AccessRules.CanPublishPlaybook(currentUser, playbook.Unit),
                Formations = playbook.Formations
                    .OrderBy(f => f.Id)
                    .Select(f => new FormationSummaryDto
                    {
                        Id = f.Id,
                        Name = f.Name,
                        DiagramJson = f.DiagramJson,
                        PlayCount = f.Plays!.Count,
                        HasPendingChange = pendingFormationIds.Contains(f.Id)
                    })
                    .ToList()
            };
        }

        public async Task<FormationDto?> GetFormationAsync(CurrentUser currentUser, int id)
        {
            var formation = await _context.Formations
                .Include(f => f.Playbook)
                .Include(f => f.Plays)
                .FirstOrDefaultAsync(f => f.Id == id && f.TeamId == currentUser.TeamId);
            if (formation == null)
            {
                return null;
            }

            var unit = formation.Playbook!.Unit;
            EnsureCanRead(currentUser, unit);

            // Coaches are told when a change is waiting; players only ever see the approved version
            var pending = currentUser.IsCoach
                ? await _context.ApprovalRequests
                    .Where(r => r.Status == ApprovalStatus.Pending && r.FormationId == formation.Id)
                    .Select(r => new { r.Id, r.TargetType, r.PlayId })
                    .ToListAsync()
                : null;

            bool putAway = IsPutAway(currentUser, formation.Playbook);

            return new FormationDto
            {
                Id = formation.Id,
                PlaybookId = formation.PlaybookId,
                PlaybookName = formation.Playbook.Name,
                Unit = unit,
                Name = formation.Name,
                DiagramJson = formation.DiagramJson,
                Version = formation.Version,
                Rules = formation.Playbook.Rules,
                IsPutAway = putAway,
                CanChange = !putAway && AccessRules.CanChangePlaybook(currentUser, unit),
                CanPublish = !putAway && AccessRules.CanPublishPlaybook(currentUser, unit),
                PendingRequestId = pending?.FirstOrDefault(r => r.TargetType == PlaybookTargetType.Formation)?.Id,
                Plays = formation.Plays!
                    .OrderBy(p => p.Name)
                    .Select(p => new PlaySummaryDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        DiagramJson = p.DiagramJson,
                        HasPendingChange = pending != null && pending.Any(r => r.PlayId == p.Id)
                    })
                    .ToList()
            };
        }

        public async Task<PlayDto?> GetPlayAsync(CurrentUser currentUser, int id)
        {
            var play = await _context.Plays
                .Include(p => p.Formation!)
                    .ThenInclude(f => f.Playbook)
                .FirstOrDefaultAsync(p => p.Id == id && p.TeamId == currentUser.TeamId);
            if (play == null)
            {
                return null;
            }

            var playbook = play.Formation!.Playbook!;
            EnsureCanRead(currentUser, playbook.Unit);

            int? pendingRequestId = currentUser.IsCoach
                ? await _context.ApprovalRequests
                    .Where(r => r.Status == ApprovalStatus.Pending && r.PlayId == play.Id)
                    .Select(r => (int?)r.Id)
                    .FirstOrDefaultAsync()
                : null;

            bool putAway = IsPutAway(currentUser, playbook);

            return new PlayDto
            {
                Id = play.Id,
                FormationId = play.FormationId,
                FormationName = play.Formation.Name,
                PlaybookId = playbook.Id,
                PlaybookName = playbook.Name,
                Unit = playbook.Unit,
                Name = play.Name,
                DiagramJson = play.DiagramJson,
                Notes = play.Notes,
                Version = play.Version,
                UpdatedAt = play.UpdatedAt,
                Rules = playbook.Rules,
                IsPutAway = putAway,
                CanChange = !putAway && AccessRules.CanChangePlaybook(currentUser, playbook.Unit),
                CanPublish = !putAway && AccessRules.CanPublishPlaybook(currentUser, playbook.Unit),
                PendingRequestId = pendingRequestId
            };
        }

        // ---------- Changing formations ----------
        //
        // Every change below is handed in through PlaybookChangeApplier.SubmitAsync, the path every change takes:
        // it goes live at once for someone who may publish in the playbook, and waits for the head coach otherwise.

        public async Task<PlaybookChangeResultDto?> CreateFormationAsync(CurrentUser currentUser, SaveFormationDto dto)
        {
            var playbook = await _context.Playbooks
                .FirstOrDefaultAsync(p => p.Id == dto.PlaybookId && p.TeamId == currentUser.TeamId);
            if (playbook == null)
            {
                return null;
            }

            PlaybookChangeApplier.EnsureCanChange(currentUser, playbook);

            var name = dto.Name.Trim();
            return await _applier.SubmitAsync(currentUser, playbook, new ApprovalRequest
            {
                TargetType = PlaybookTargetType.Formation,
                Action = PlaybookChangeAction.Create,
                TargetName = name,
                ProposedName = name,
                ProposedJson = DiagramValidator.Normalize(dto.DiagramJson, playbook.Unit.ToString(), isFormation: true, rules: playbook.Rules)
            });
        }

        public async Task<PlaybookChangeResultDto?> UpdateFormationAsync(CurrentUser currentUser, int id, SaveFormationDto dto)
        {
            var formation = await FindFormationAsync(currentUser, id);
            if (formation == null)
            {
                return null;
            }

            var playbook = formation.Playbook!;
            PlaybookChangeApplier.EnsureCanChange(currentUser, playbook);
            EnsureStartedFromCurrentVersion(dto.Version, formation.Version, formation.Name);

            return await _applier.SubmitAsync(currentUser, playbook, new ApprovalRequest
            {
                TargetType = PlaybookTargetType.Formation,
                Action = PlaybookChangeAction.Update,
                FormationId = formation.Id,
                BaseVersion = formation.Version,
                TargetName = formation.Name,
                ProposedName = dto.Name.Trim(),
                ProposedJson = DiagramValidator.Normalize(dto.DiagramJson, playbook.Unit.ToString(), isFormation: true, rules: playbook.Rules)
            });
        }

        public async Task<PlaybookChangeResultDto?> DeleteFormationAsync(CurrentUser currentUser, int id)
        {
            var formation = await FindFormationAsync(currentUser, id);
            if (formation == null)
            {
                return null;
            }

            var playbook = formation.Playbook!;
            PlaybookChangeApplier.EnsureCanChange(currentUser, playbook);

            return await _applier.SubmitAsync(currentUser, playbook, new ApprovalRequest
            {
                TargetType = PlaybookTargetType.Formation,
                Action = PlaybookChangeAction.Delete,
                FormationId = formation.Id,
                BaseVersion = formation.Version,
                TargetName = formation.Name
            });
        }

        // ---------- Changing plays ----------

        public async Task<PlaybookChangeResultDto?> CreatePlayAsync(CurrentUser currentUser, SavePlayDto dto)
        {
            var formation = await FindFormationAsync(currentUser, dto.FormationId);
            if (formation == null)
            {
                return null;
            }

            var playbook = formation.Playbook!;
            PlaybookChangeApplier.EnsureCanChange(currentUser, playbook);

            var name = dto.Name.Trim();
            return await _applier.SubmitAsync(currentUser, playbook, new ApprovalRequest
            {
                TargetType = PlaybookTargetType.Play,
                Action = PlaybookChangeAction.Create,
                FormationId = formation.Id,
                TargetName = name,
                ProposedName = name,
                ProposedJson = DiagramValidator.Normalize(dto.DiagramJson, playbook.Unit.ToString(), isFormation: false, rules: playbook.Rules),
                ProposedNotes = CleanNotes(dto.Notes)
            });
        }

        public async Task<PlaybookChangeResultDto?> UpdatePlayAsync(CurrentUser currentUser, int id, SavePlayDto dto)
        {
            var play = await FindPlayAsync(currentUser, id);
            if (play == null)
            {
                return null;
            }

            var playbook = play.Formation!.Playbook!;
            PlaybookChangeApplier.EnsureCanChange(currentUser, playbook);
            EnsureStartedFromCurrentVersion(dto.Version, play.Version, play.Name);

            return await _applier.SubmitAsync(currentUser, playbook, new ApprovalRequest
            {
                TargetType = PlaybookTargetType.Play,
                Action = PlaybookChangeAction.Update,
                FormationId = play.FormationId,
                PlayId = play.Id,
                BaseVersion = play.Version,
                TargetName = play.Name,
                ProposedName = dto.Name.Trim(),
                ProposedJson = DiagramValidator.Normalize(dto.DiagramJson, playbook.Unit.ToString(), isFormation: false, rules: playbook.Rules),
                ProposedNotes = CleanNotes(dto.Notes)
            });
        }

        public async Task<PlaybookChangeResultDto?> DeletePlayAsync(CurrentUser currentUser, int id)
        {
            var play = await FindPlayAsync(currentUser, id);
            if (play == null)
            {
                return null;
            }

            var playbook = play.Formation!.Playbook!;
            PlaybookChangeApplier.EnsureCanChange(currentUser, playbook);

            return await _applier.SubmitAsync(currentUser, playbook, new ApprovalRequest
            {
                TargetType = PlaybookTargetType.Play,
                Action = PlaybookChangeAction.Delete,
                FormationId = play.FormationId,
                PlayId = play.Id,
                BaseVersion = play.Version,
                TargetName = play.Name
            });
        }

        // ---------- Helpers ----------

        private async Task<Formation?> FindFormationAsync(CurrentUser currentUser, int id)
        {
            return await _context.Formations
                .Include(f => f.Playbook)
                .FirstOrDefaultAsync(f => f.Id == id && f.TeamId == currentUser.TeamId);
        }

        private async Task<Play?> FindPlayAsync(CurrentUser currentUser, int id)
        {
            return await _context.Plays
                .Include(p => p.Formation!)
                    .ThenInclude(f => f.Playbook)
                .FirstOrDefaultAsync(p => p.Id == id && p.TeamId == currentUser.TeamId);
        }

        // The formations of a playbook that have a change waiting, shown to coaches only
        private async Task<List<int>> PendingFormationIdsAsync(CurrentUser currentUser, int playbookId)
        {
            if (!currentUser.IsCoach)
            {
                return new List<int>();
            }

            return await _context.ApprovalRequests
                .Where(r => r.PlaybookId == playbookId
                    && r.Status == ApprovalStatus.Pending
                    && r.TargetType == PlaybookTargetType.Formation
                    && r.FormationId != null)
                .Select(r => r.FormationId!.Value)
                .ToListAsync();
        }

        private static void EnsureCanRead(CurrentUser currentUser, TeamUnit unit)
        {
            if (!AccessRules.CanReadPlaybook(currentUser, unit))
            {
                throw new ForbiddenException($"The {Labels.Unit(unit).ToLowerInvariant()} playbook is shared with the players of that unit's groups.");
            }
        }

        // A playbook belongs to one game: 11 players a side in American football, 12 in Canadian football
        private static bool IsPutAway(CurrentUser currentUser, Playbook playbook)
        {
            return playbook.Rules != currentUser.Rules;
        }

        // Business rule: a change is drawn on top of one version. If someone else changed the formation or play
        // after the designer was opened, saving would silently undo their work, so it is refused.
        private static void EnsureStartedFromCurrentVersion(int? startedFrom, int current, string name)
        {
            if (startedFrom != null && startedFrom != current)
            {
                throw new InvalidOperationException(
                    $"{name} was changed by someone else while you were working on it. Open it again to start from the current version.");
            }
        }

        private static string? CleanNotes(string? notes)
        {
            return string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        }
    }
}
