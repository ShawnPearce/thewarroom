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

            var sentAgain = await SentAgainAsync(currentUser.TeamId);

            return requests
                .Where(r => CanSee(currentUser, r))
                .Select(r => ToDto(new ApprovalRequestDto(), r, currentUser, SentAgainAs(sentAgain, r.Id)))
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

            var sentAgain = await SentAgainAsync(currentUser.TeamId);

            var detail = (ApprovalRequestDetailDto)ToDto(new ApprovalRequestDetailDto(), request, currentUser, SentAgainAs(sentAgain, request.Id));
            detail.FormationName = request.Formation?.Name;
            detail.ProposedJson = request.ProposedJson;
            detail.ProposedNotes = request.ProposedNotes;
            detail.CanPublish = request.Playbook!.Rules == currentUser.Rules
                && AccessRules.CanPublishPlaybook(currentUser, request.Playbook.Unit);

            // A request that was sent again shows what the head coach said about the one it was made from
            if (request.ResubmittedFromId != null)
            {
                var earlier = await FindAsync(currentUser, request.ResubmittedFromId.Value);
                if (earlier != null)
                {
                    detail.EarlierReviewNote = earlier.ReviewNote;
                    detail.EarlierReviewedBy = earlier.ReviewedBy != null ? Labels.FullName(earlier.ReviewedBy) : null;
                    detail.EarlierReviewedAt = earlier.ReviewedAt;
                }
            }

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
            }
            else if (request.TargetType == PlaybookTargetType.Formation && request.Formation != null
                && request.Action != PlaybookChangeAction.Create)
            {
                detail.BeforeName = request.Formation.Name;
                detail.BeforeJson = request.Formation.DiagramJson;
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

        public async Task<ApprovalNoticesDto> GetNoticesAsync(CurrentUser currentUser)
        {
            EnsureIsCoach(currentUser);

            // As in the list, only the playbooks of the game the team plays count
            var requests = _context.ApprovalRequests
                .Where(r => r.TeamId == currentUser.TeamId && r.Playbook!.Rules == currentUser.Rules);

            return new ApprovalNoticesDto
            {
                // The head coach is told what is waiting for a decision
                WaitingForDecision = AccessRules.CanReviewPlaybookChanges(currentUser)
                    ? await requests.CountAsync(r => r.Status == ApprovalStatus.Pending)
                    : 0,

                // A coach is told about their own changes that were rejected, until they have opened them
                SentBack = await requests.CountAsync(r => r.Status == ApprovalStatus.Rejected
                    && r.RequestedById == currentUser.Id
                    && r.DecisionSeenAt == null)
            };
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

        // ---------- The requester, after the head coach rejected the request ----------

        public async Task<bool> MarkSeenAsync(CurrentUser currentUser, int id)
        {
            var request = await FindAsync(currentUser, id);
            if (request == null)
            {
                return false;
            }

            EnsureIsRequester(currentUser, request);

            // Only a rejection is news to the coach, and reading it a second time changes nothing
            if (request.Status == ApprovalStatus.Rejected && request.DecisionSeenAt == null)
            {
                request.DecisionSeenAt = _clock.GetUtcNow().UtcDateTime;
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<PlaybookChangeResultDto?> ResubmitAsync(CurrentUser currentUser, int id, ReviseApprovalRequestDto dto)
        {
            var rejected = await FindAsync(currentUser, id);
            if (rejected == null)
            {
                return null;
            }

            EnsureIsRequester(currentUser, rejected);

            // Business rule: only a change the head coach rejected is sent again. One that is still waiting is revised instead.
            if (rejected.Status == ApprovalStatus.Pending)
            {
                throw new InvalidOperationException("This request is still waiting for the head coach. Revise it instead.");
            }
            if (rejected.Status != ApprovalStatus.Rejected)
            {
                throw new InvalidOperationException(
                    $"Only a request the head coach rejected can be sent again. This one was {rejected.Status.ToString().ToLowerInvariant()}.");
            }

            // Business rule: a request to delete has no drawing to revise
            if (rejected.Action == PlaybookChangeAction.Delete)
            {
                throw new InvalidOperationException("A request to delete has nothing to revise. Ask for the delete again from the playbook.");
            }

            // Business rule: a rejected request is sent again once. After that, the newer request is the one to work on.
            bool sentAgain = await _context.ApprovalRequests
                .AnyAsync(r => r.ResubmittedFromId == rejected.Id && r.Status != ApprovalStatus.Withdrawn);
            if (sentAgain)
            {
                throw new InvalidOperationException("A new version of this request has already been sent.");
            }

            var playbook = rejected.Playbook!;
            PlaybookChangeApplier.EnsureCanChange(currentUser, playbook);

            // Business rule: a change is made against one version. If the formation or play changed after the request
            // was made, the drawing is of an older version, so the change has to be made again on the current one.
            if (IsStale(rejected))
            {
                throw new InvalidOperationException(rejected.Action == PlaybookChangeAction.Create
                    ? "The formation this play was drawn on no longer exists."
                    : $"{rejected.TargetName} has changed since this request was made. Open it in the playbook and make your change on the current version.");
            }

            bool isFormation = rejected.TargetType == PlaybookTargetType.Formation;
            string name = dto.Name.Trim();

            // The same change, with the coach's new drawing, pointing back at the request that was rejected
            var again = new ApprovalRequest
            {
                TargetType = rejected.TargetType,
                Action = rejected.Action,
                FormationId = rejected.FormationId,
                PlayId = rejected.PlayId,
                BaseVersion = rejected.BaseVersion,
                TargetName = rejected.Action == PlaybookChangeAction.Create ? name : rejected.TargetName,
                ProposedName = name,
                ProposedJson = DiagramValidator.Normalize(dto.DiagramJson, playbook.Unit.ToString(), isFormation, playbook.Rules),
                ProposedNotes = isFormation || string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                ResubmittedFromId = rejected.Id
            };

            // Sending it again means the rejection has been read
            rejected.DecisionSeenAt ??= _clock.GetUtcNow().UtcDateTime;

            // From here on it is a change like any other: live at once if this coach may publish by now,
            // otherwise waiting for the head coach
            return await _applier.SubmitAsync(currentUser, playbook, again);
        }

        // ---------- Helpers ----------

        // The requests that were made from a rejected one, by the id of the rejected one.
        // One that was withdrawn again does not count, so the rejected request can then be sent again.
        private async Task<Dictionary<int, int>> SentAgainAsync(int teamId)
        {
            var pairs = await _context.ApprovalRequests
                .Where(r => r.TeamId == teamId && r.ResubmittedFromId != null && r.Status != ApprovalStatus.Withdrawn)
                .OrderBy(r => r.Id)
                .Select(r => new { RejectedId = r.ResubmittedFromId!.Value, r.Id })
                .ToListAsync();

            var sentAgain = new Dictionary<int, int>();
            foreach (var pair in pairs)
            {
                sentAgain[pair.RejectedId] = pair.Id;
            }

            return sentAgain;
        }

        private static int? SentAgainAs(Dictionary<int, int> sentAgain, int rejectedId)
        {
            return sentAgain.TryGetValue(rejectedId, out int newId) ? newId : null;
        }

        // True when what the request was made against has changed since, or is no longer there
        private static bool IsStale(ApprovalRequest request)
        {
            if (request.Action == PlaybookChangeAction.Create)
            {
                // A new play needs the formation it was drawn on; a new formation needs nothing
                return request.TargetType == PlaybookTargetType.Play && request.Formation == null;
            }

            if (request.TargetType == PlaybookTargetType.Play)
            {
                return request.Play == null || request.Play.Version != request.BaseVersion;
            }

            return request.Formation == null || request.Formation.Version != request.BaseVersion;
        }

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
                throw new ForbiddenException("Only the coach who made a request can revise it, withdraw it or send it again.");
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

        // Map entity → DTO (works for the list row and for the detail, which adds to it).
        // "sentAgainAs" is the request that was made from this one after it was rejected, if there is one.
        private static ApprovalRequestDto ToDto(ApprovalRequestDto dto, ApprovalRequest request, CurrentUser currentUser, int? sentAgainAs)
        {
            bool pending = request.Status == ApprovalStatus.Pending;
            bool rejected = request.Status == ApprovalStatus.Rejected;
            bool own = request.RequestedById == currentUser.Id;

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
            dto.ResubmittedFromId = request.ResubmittedFromId;
            dto.ResubmittedAsId = sentAgainAs;
            dto.IsStale = (pending || rejected) && IsStale(request);
            dto.CanReview = pending && AccessRules.CanReviewPlaybookChanges(currentUser);
            dto.CanRevise = pending && own;

            // A rejected change can be revised and sent again by the coach who made it, as long as it has a drawing,
            // was not sent again already, and still fits the playbook the coach may change
            dto.CanResubmit = rejected && own
                && request.Action != PlaybookChangeAction.Delete
                && sentAgainAs == null
                && !dto.IsStale
                && request.Playbook.Rules == currentUser.Rules
                && AccessRules.CanChangePlaybook(currentUser, request.Playbook.Unit);
            dto.IsNewDecision = rejected && own && request.DecisionSeenAt == null;
            return dto;
        }
    }
}
