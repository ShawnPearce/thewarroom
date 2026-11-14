using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class StudyTimeService : IStudyTimeService
    {
        // One report can add a minute at most. Pages report every 15 seconds, so an honest page never comes close.
        public const int MaxPerReportMs = 60_000;

        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public StudyTimeService(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        public async Task<StudyCreditDto?> ReportAsync(CurrentUser currentUser, StudyReportDto dto)
        {
            // Permission: study time is recorded for players
            if (!currentUser.IsPlayer)
            {
                throw new ForbiddenException("Study time is recorded for players only.");
            }

            // Business rule: time is counted only on something the player is allowed to open
            if (dto.Kind == ViewingKind.Playbook)
            {
                var playbook = await _context.Playbooks
                    .FirstOrDefaultAsync(p => p.Id == dto.PlaybookId && p.TeamId == currentUser.TeamId);
                if (playbook == null)
                {
                    return null;
                }
                if (!AccessRules.CanReadPlaybook(currentUser, playbook.Unit))
                {
                    throw new ForbiddenException("This playbook is not shared with you.");
                }
            }
            else
            {
                var cutup = await _context.Cutups
                    .Include(c => c.Assignments)
                    .FirstOrDefaultAsync(c => c.Id == dto.CutupId && c.TeamId == currentUser.TeamId);
                if (cutup == null)
                {
                    return null;
                }
                if (!AccessRules.CanWatchCutup(currentUser, cutup.Assignments!))
                {
                    throw new ForbiddenException("This film has not been shared with you.");
                }
            }

            int? cutupId = dto.Kind == ViewingKind.Film ? dto.CutupId : null;
            int? playbookId = dto.Kind == ViewingKind.Playbook ? dto.PlaybookId : null;

            // The check and the update below happen as one step, so that two pages reporting
            // at the same moment cannot both be credited for the same seconds.
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var now = _clock.GetUtcNow().UtcDateTime;

            // Business rule: viewing time is credited no faster than the clock runs.
            // However many pages a player has open, the time since their previous credit is the most
            // that can be added now, and one report adds a minute at most.
            var previousCredit = await _context.ViewingSessions
                .Where(v => v.UserId == currentUser.Id)
                .MaxAsync(v => (DateTime?)v.LastCreditAt);

            int allowedMs = MaxPerReportMs;
            if (previousCredit != null)
            {
                double sinceThen = Math.Max(0, (now - previousCredit.Value).TotalMilliseconds);
                allowedMs = (int)Math.Min(MaxPerReportMs, sinceThen);
            }

            int creditMs = Math.Clamp(dto.ActiveMs, 0, allowedMs);

            // Every report from one page goes to the same row; the page's first report opens it
            var session = await _context.ViewingSessions
                .FirstOrDefaultAsync(v => v.UserId == currentUser.Id && v.PageKey == dto.PageKey);

            if (session == null)
            {
                session = new ViewingSession
                {
                    TeamId = currentUser.TeamId,
                    UserId = currentUser.Id,
                    Kind = dto.Kind,
                    CutupId = cutupId,
                    PlaybookId = playbookId,
                    PageKey = dto.PageKey,
                    LoginSessionId = currentUser.LoginSessionId,
                    StartedAt = now.AddMilliseconds(-creditMs),   // the viewing began before it was reported
                    LastCreditAt = now,
                    ActiveMs = creditMs
                };
                _context.ViewingSessions.Add(session);
            }
            else
            {
                // Business rule: a page reports for one thing only, so film time can never be moved to the playbook or back
                if (session.Kind != dto.Kind || session.CutupId != cutupId || session.PlaybookId != playbookId)
                {
                    throw new InvalidOperationException("This page is already counting time for something else.");
                }

                if (creditMs > 0)
                {
                    session.ActiveMs += creditMs;
                    session.LastCreditAt = now;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new StudyCreditDto { CreditedMs = creditMs, PageTotalMs = session.ActiveMs };
        }
    }
}
