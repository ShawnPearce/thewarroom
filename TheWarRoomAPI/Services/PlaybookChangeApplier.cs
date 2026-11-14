using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // The one place where a playbook change is checked against the playbook and applied to it.
    // Both ways a change becomes live go through here: a coach publishing directly, and the head coach approving.
    // That keeps the rules identical in both cases.
    public class PlaybookChangeApplier
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public PlaybookChangeApplier(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        // Checks that the change still fits the playbook as it is right now.
        // Run when the request is made and again when it is approved, because the playbook may have changed in between.
        public async Task ValidateAsync(ApprovalRequest request)
        {
            if (request.TargetType == PlaybookTargetType.Formation)
            {
                await ValidateFormationChangeAsync(request);
            }
            else
            {
                await ValidatePlayChangeAsync(request);
            }
        }

        // Makes the change live. The caller saves.
        public async Task ApplyAsync(ApprovalRequest request)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            if (request.TargetType == PlaybookTargetType.Formation)
            {
                await ApplyFormationChangeAsync(request, now);
            }
            else
            {
                await ApplyPlayChangeAsync(request, now);
            }
        }

        // ---------- Formations ----------

        private async Task ValidateFormationChangeAsync(ApprovalRequest request)
        {
            if (request.Action == PlaybookChangeAction.Create)
            {
                await EnsureFormationNameIsFreeAsync(request.PlaybookId, request.ProposedName!, 0);
                return;
            }

            var formation = await _context.Formations.FirstOrDefaultAsync(f => f.Id == request.FormationId);
            if (formation == null)
            {
                throw new InvalidOperationException($"The formation {request.TargetName} no longer exists.");
            }

            // Business rule: a change is made against one version; if the formation changed since, it has to be redone
            if (request.BaseVersion != formation.Version)
            {
                throw new InvalidOperationException($"{formation.Name} has changed since this request was made.");
            }

            if (request.Action == PlaybookChangeAction.Update)
            {
                await EnsureFormationNameIsFreeAsync(request.PlaybookId, request.ProposedName!, formation.Id);
            }
            else
            {
                // Business rule: a formation can only be removed when it has no plays
                bool hasPlays = await _context.Plays.AnyAsync(p => p.FormationId == formation.Id);
                if (hasPlays)
                {
                    throw new InvalidOperationException($"{formation.Name} still has plays. Delete its plays first.");
                }
            }
        }

        private async Task ApplyFormationChangeAsync(ApprovalRequest request, DateTime now)
        {
            if (request.Action == PlaybookChangeAction.Create)
            {
                request.Formation = new Formation
                {
                    TeamId = request.TeamId,
                    PlaybookId = request.PlaybookId,
                    Name = request.ProposedName!,
                    DiagramJson = request.ProposedJson!,
                    Version = 1,
                    UpdatedAt = now
                };
                _context.Formations.Add(request.Formation);
                return;
            }

            var formation = await _context.Formations.FirstAsync(f => f.Id == request.FormationId);

            // Remember what is being replaced, for the history
            request.PreviousName = formation.Name;
            request.PreviousJson = formation.DiagramJson;

            if (request.Action == PlaybookChangeAction.Update)
            {
                formation.Name = request.ProposedName!;
                formation.DiagramJson = request.ProposedJson!;
                formation.Version++;
                formation.UpdatedAt = now;
            }
            else
            {
                request.FormationId = null;
                _context.Formations.Remove(formation);
            }
        }

        // ---------- Plays ----------

        private async Task ValidatePlayChangeAsync(ApprovalRequest request)
        {
            if (request.Action == PlaybookChangeAction.Create)
            {
                // Business rule: a play is added under a formation that exists (that is, an approved one)
                bool formationExists = await _context.Formations.AnyAsync(f => f.Id == request.FormationId);
                if (!formationExists)
                {
                    throw new InvalidOperationException("The formation this play was drawn on no longer exists.");
                }

                await EnsurePlayNameIsFreeAsync(request.FormationId!.Value, request.ProposedName!, 0);
                return;
            }

            var play = await _context.Plays.FirstOrDefaultAsync(p => p.Id == request.PlayId);
            if (play == null)
            {
                throw new InvalidOperationException($"The play {request.TargetName} no longer exists.");
            }

            // Business rule: a change is made against one version; if the play changed since, it has to be redone
            if (request.BaseVersion != play.Version)
            {
                throw new InvalidOperationException($"{play.Name} has changed since this request was made.");
            }

            if (request.Action == PlaybookChangeAction.Update)
            {
                await EnsurePlayNameIsFreeAsync(play.FormationId, request.ProposedName!, play.Id);
            }
        }

        private async Task ApplyPlayChangeAsync(ApprovalRequest request, DateTime now)
        {
            if (request.Action == PlaybookChangeAction.Create)
            {
                request.Play = new Play
                {
                    TeamId = request.TeamId,
                    FormationId = request.FormationId!.Value,
                    Name = request.ProposedName!,
                    DiagramJson = request.ProposedJson!,
                    Notes = request.ProposedNotes,
                    Version = 1,
                    UpdatedAt = now
                };
                _context.Plays.Add(request.Play);
                return;
            }

            var play = await _context.Plays.FirstAsync(p => p.Id == request.PlayId);

            // Remember what is being replaced, for the history
            request.PreviousName = play.Name;
            request.PreviousJson = play.DiagramJson;
            request.PreviousNotes = play.Notes;

            if (request.Action == PlaybookChangeAction.Update)
            {
                play.Name = request.ProposedName!;
                play.DiagramJson = request.ProposedJson!;
                play.Notes = request.ProposedNotes;
                play.Version++;
                play.UpdatedAt = now;
            }
            else
            {
                request.PlayId = null;
                _context.Plays.Remove(play);
            }
        }

        // ---------- Names ----------

        // Business rule: two formations of the same playbook cannot share a name
        private async Task EnsureFormationNameIsFreeAsync(int playbookId, string name, int exceptFormationId)
        {
            bool taken = await _context.Formations
                .AnyAsync(f => f.PlaybookId == playbookId && f.Name == name && f.Id != exceptFormationId);
            if (taken)
            {
                throw new InvalidOperationException($"The playbook already has a formation called {name}.");
            }
        }

        // Business rule: two plays of the same formation cannot share a name
        private async Task EnsurePlayNameIsFreeAsync(int formationId, string name, int exceptPlayId)
        {
            bool taken = await _context.Plays
                .AnyAsync(p => p.FormationId == formationId && p.Name == name && p.Id != exceptPlayId);
            if (taken)
            {
                throw new InvalidOperationException($"This formation already has a play called {name}.");
            }
        }
    }
}
