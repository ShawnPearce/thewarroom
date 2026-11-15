using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.Diagrams;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class ApprovalService : IApprovalService
    {
        private readonly AppDbContext _context;
        private readonly PlaybookChangeApplier _applier;
        private readonly TimeProvider _clock;

        public ApprovalService(AppDbContext context, PlaybookChangeApplier applier, TimeProvider clock)
        {
            _context = context;
            _applier = applier;
            _clock = clock;
        }

        // ---------- Reading ----------

        public async Task<List<ApprovalRequestDto>> GetRequestsAsync(CurrentUser currentUser, ApprovalStatus? status)
        {
            EnsureIsCoach(currentUser);

            // The list keeps to the playbooks of the game the team plays; requests in the other game's playbooks
            // are put away with them
            var query = RequestsOfTeam(currentUser.TeamId).Where(r => r.Playbook!.Rules == currentUser.Rules);
            if (status != null)
            {
                query = query.Where(r => r.Status == status);
            }

            var requests = await query
                .OrderByDescending(r => r.RequestedAt)
                .ThenByDescending(r => r.Id)
                .ToListAsync();

            return requests
                .Where(r => CanSee(currentUser, r))
                .Select(r => ToDto(new ApprovalRequestDto(), r, currentUser))
                .ToList();
        }

        public async Task<ApprovalRequestDetailDto?> GetRequestAsync(CurrentUser currentUser, int id)
        {
            EnsureIsCoach(currentUser);

            var request = await RequestsOfTeam(currentUser.TeamId).FirstOrDefaultAsync(r => r.Id == id);
            if (request == null)
            {
                return null;
            }

            if (!CanSee(currentUser, request))
            {
                throw new ForbiddenException("This request is about a playbook outside the ones you can change.");
            }

            var detail = (ApprovalRequestDetailDto)ToDto(new ApprovalRequestDetailDto(), request, currentUser);
            detail.FormationName = request.Formation?.Name;
            detail.ProposedJson = request.ProposedJson;
            detail.ProposedNotes = request.ProposedNotes;

            if (request.Status == ApprovalStatus.Approved)
            {
                // History: what the change replaced, as remembered when it was applied
                detail.BeforeName = request.PreviousName;
                detail.BeforeJson = request.PreviousJson;
                detail.BeforeNotes = request.PreviousNotes;
            }
            else if (request.TargetType == PlaybookTargetType.Play && request.Play != null)
            {
                detail.BeforeName = request.Play.Name;
                detail.BeforeJson = request.Play.DiagramJson;
                detail.BeforeNotes = request.Play.Notes;
                detail.IsStale = request.Status == ApprovalStatus.Pending && request.BaseVersion != request.Play.Version;
            }
            else if (request.TargetType == PlaybookTargetType.Formation && request.Formation != null
                && request.Action != PlaybookChangeAction.Create)
            {
                detail.BeforeName = request.Formation.Name;
                detail.BeforeJson = request.Formation.DiagramJson;
                detail.IsStale = request.Status == ApprovalStatus.Pending && request.BaseVersion != request.Formation.Version;
            }

            // A new play is compared with the formation it is drawn on
            if (request.TargetType == PlaybookTargetType.Play && request.Action == PlaybookChangeAction.Create
                && request.Formation != null && detail.BeforeJson == null)
            {
                detail.BeforeName = $"{request.Formation.Name} (formation)";
                detail.BeforeJson = request.Formation.DiagramJson;
            }

            return detail;
        }

        // ---------- The head coach's decision ----------

        public async Task<bool> ApproveAsync(CurrentUser currentUser, int id, ReviewDecisionDto dto)
        {
            var request = await FindAsync(currentUser, id);
            if (request == null)
            {
                return false;
            }

            EnsureCanReview(currentUser);
            EnsureIsPending(request);

            // The playbook may have changed while the request was waiting, so the rules are checked again.
            // Then the change goes live through the same code a direct publish uses.
            await _applier.ValidateAsync(request);
            await _applier.ApplyAsync(request);

            request.Status = ApprovalStatus.Approved;
            request.ReviewedById = currentUser.Id;
            request.ReviewedAt = _clock.GetUtcNow().UtcDateTime;
            request.ReviewNote = CleanNote(dto.Note);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectAsync(CurrentUser currentUser, int id, ReviewDecisionDto dto)
        {
            var request = await FindAsync(currentUser, id);
            if (request == null)
            {
                return false;
            }

            EnsureCanReview(currentUser);
            EnsureIsPending(request);

            // Nothing in the playbook changes: the approved version simply stays as it is
            request.Status = ApprovalStatus.Rejected;
            request.ReviewedById = currentUser.Id;
            request.ReviewedAt = _clock.GetUtcNow().UtcDateTime;
            request.ReviewNote = CleanNote(dto.Note);

            await _context.SaveChangesAsync();
            return true;
        }

        // ---------- The requester, while the request is waiting ----------

        public async Task<bool> ReviseAsync(CurrentUser currentUser, int id, ReviseApprovalRequestDto dto)
        {
            var request = await FindAsync(currentUser, id);
            if (request == null)
            {
                return false;
            }

            EnsureIsRequester(currentUser, request);
            EnsureIsPending(request);

            // Business rule: a delete request has nothing to revise
            if (request.Action == PlaybookChangeAction.Delete)
            {
                throw new InvalidOperationException("A request to delete cannot be revised. Withdraw it instead.");
            }

            bool isFormation = request.TargetType == PlaybookTargetType.Formation;
            request.ProposedName = dto.Name.Trim();
            request.ProposedJson = DiagramValidator.Normalize(dto.DiagramJson, request.Playbook!.Unit.ToString(), isFormation, request.Playbook.Rules);
            request.ProposedNotes = isFormation || string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            if (request.Action == PlaybookChangeAction.Create)
            {
                request.TargetName = request.ProposedName;
            }

            await _applier.ValidateAsync(request);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> WithdrawAsync(CurrentUser currentUser, int id)
        {
            var request = await FindAsync(currentUser, id);
            if (request == null)
            {
                return false;
            }

            EnsureIsRequester(currentUser, request);
            EnsureIsPending(request);

            request.Status = ApprovalStatus.Withdrawn;
            request.ReviewedAt = _clock.GetUtcNow().UtcDateTime;

            await _context.SaveChangesAsync();
            return true;
        }

        // ---------- Helpers ----------

        private IQueryable<ApprovalRequest> RequestsOfTeam(int teamId)
        {
            return _context.ApprovalRequests
                .Where(r => r.TeamId == teamId)
                .Include(r => r.Playbook)
                .Include(r => r.Formation)
                .Include(r => r.Play)
                .Include(r => r.RequestedBy)
                .Include(r => r.ReviewedBy);
        }

        private async Task<ApprovalRequest?> FindAsync(CurrentUser currentUser, int id)
        {
            return await RequestsOfTeam(currentUser.TeamId).FirstOrDefaultAsync(r => r.Id == id);
        }

        // The head coach sees every request; another coach sees their own and those in playbooks they can change
        private static bool CanSee(CurrentUser currentUser, ApprovalRequest request)
        {
            return currentUser.IsHeadCoach
                || request.RequestedById == currentUser.Id
                || AccessRules.CanChangePlaybook(currentUser, request.Playbook!.Unit);
        }

        private static void EnsureIsCoach(CurrentUser currentUser)
        {
            if (!currentUser.IsCoach)
            {
                throw new ForbiddenException("Only coaches can see playbook change requests.");
            }
        }

        private static void EnsureCanReview(CurrentUser currentUser)
        {
            if (!AccessRules.CanReviewPlaybookChanges(currentUser))
            {
                throw new ForbiddenException("Only the head coach can approve or reject playbook changes.");
            }
        }

        private static void EnsureIsRequester(CurrentUser currentUser, ApprovalRequest request)
        {
            if (request.RequestedById != currentUser.Id)
            {
                throw new ForbiddenException("Only the coach who made a request can revise or withdraw it.");
            }
        }

        // Business rule: only a request that is still waiting can be decided, revised or withdrawn
        private static void EnsureIsPending(ApprovalRequest request)
        {
            if (request.Status != ApprovalStatus.Pending)
            {
                throw new InvalidOperationException($"This request has already been {request.Status.ToString().ToLowerInvariant()}.");
            }
        }

        private static string? CleanNote(string? note)
        {
            return string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        }

        // Map entity → DTO (works for the list row and for the detail, which adds to it)
        private static ApprovalRequestDto ToDto(ApprovalRequestDto dto, ApprovalRequest request, CurrentUser currentUser)
        {
            bool pending = request.Status == ApprovalStatus.Pending;

            dto.Id = request.Id;
            dto.PlaybookId = request.PlaybookId;
            dto.PlaybookName = request.Playbook!.Name;
            dto.Unit = request.Playbook.Unit;
            dto.TargetType = request.TargetType;
            dto.Action = request.Action;
            dto.FormationId = request.FormationId;
            dto.PlayId = request.PlayId;
            dto.TargetName = request.TargetName;
            dto.ProposedName = request.ProposedName;
            dto.Status = request.Status;
            dto.AutoApproved = request.AutoApproved;
            dto.RequestedById = request.RequestedById;
            dto.RequestedBy = Labels.FullName(request.RequestedBy!);
            dto.RequestedAt = request.RequestedAt;
            dto.ReviewedBy = request.ReviewedBy != null ? Labels.FullName(request.ReviewedBy) : null;
            dto.ReviewedAt = request.ReviewedAt;
            dto.ReviewNote = request.ReviewNote;
            dto.CanReview = pending && AccessRules.CanReviewPlaybookChanges(currentUser);
            dto.CanRevise = pending && request.RequestedById == currentUser.Id;
            return dto;
        }
    }
}
