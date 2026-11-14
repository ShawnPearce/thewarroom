using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Viewing time: what a page reports, and how much of it the server credits
    public class StudyTimeServiceTests : IDisposable
    {
        private const int Seconds = 1000;

        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        // A team, a receiver, and the offence playbook the receiver may read
        private async Task<(CurrentUser HeadCoach, CurrentUser Player, int PlaybookId)> TeamWithAPlayerAsync()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int playbookId = await _world.PlaybookIdAsync(headCoach, TeamUnit.Offence);

            return (headCoach, player, playbookId);
        }

        // One report from a playbook page
        private async Task<StudyCreditDto> ReportAsync(CurrentUser player, int playbookId, string pageKey, int activeMs)
        {
            using var context = _world.NewContext();
            var credit = await _world.StudyTime(context).ReportAsync(player, new StudyReportDto
            {
                Kind = ViewingKind.Playbook,
                PlaybookId = playbookId,
                PageKey = pageKey,
                ActiveMs = activeMs
            });
            return credit!;
        }

        [Fact]
        public async Task ReportsFromOnePage_AddUpInOneRow()
        {
            var (headCoach, player, playbookId) = await TeamWithAPlayerAsync();

            // The page reports 15 seconds of viewing every 15 seconds
            _world.Clock.Advance(TimeSpan.FromSeconds(15));
            var first = await ReportAsync(player, playbookId, "page-one", 15 * Seconds);
            _world.Clock.Advance(TimeSpan.FromSeconds(15));
            var second = await ReportAsync(player, playbookId, "page-one", 15 * Seconds);

            Assert.Equal(15 * Seconds, first.CreditedMs);
            Assert.Equal(15 * Seconds, second.CreditedMs);
            Assert.Equal(30 * Seconds, second.PageTotalMs);

            using var context = _world.NewContext();
            var session = await context.ViewingSessions.SingleAsync();
            Assert.Equal(player.Id, session.UserId);
            Assert.Equal(ViewingKind.Playbook, session.Kind);
            Assert.Equal(playbookId, session.PlaybookId);
            Assert.Equal(30 * Seconds, session.ActiveMs);
        }

        [Fact]
        public async Task ViewingTime_IsCreditedNoFasterThanTheClockRuns()
        {
            var (headCoach, player, playbookId) = await TeamWithAPlayerAsync();
            await ReportAsync(player, playbookId, "page-one", 15 * Seconds);

            // A second report straight away claims 15 more seconds, but no time has passed
            var atOnce = await ReportAsync(player, playbookId, "page-one", 15 * Seconds);
            Assert.Equal(0, atOnce.CreditedMs);

            // Five seconds later a claim of 15 seconds is worth five
            _world.Clock.Advance(TimeSpan.FromSeconds(5));
            var later = await ReportAsync(player, playbookId, "page-one", 15 * Seconds);
            Assert.Equal(5 * Seconds, later.CreditedMs);
            Assert.Equal(20 * Seconds, later.PageTotalMs);
        }

        [Fact]
        public async Task TwoPagesOpenAtOnce_DoNotDoubleCount()
        {
            var (headCoach, player, playbookId) = await TeamWithAPlayerAsync();
            await ReportAsync(player, playbookId, "tab-one", 1 * Seconds);   // the starting point for the clock rule

            // For one minute, two tabs each report 15 seconds of viewing every 15 seconds
            for (int i = 0; i < 4; i++)
            {
                _world.Clock.Advance(TimeSpan.FromSeconds(15));
                await ReportAsync(player, playbookId, "tab-one", 15 * Seconds);
                await ReportAsync(player, playbookId, "tab-two", 15 * Seconds);
            }

            // Two minutes were claimed; one minute went by, so one minute is counted
            using var context = _world.NewContext();
            int counted = await context.ViewingSessions.SumAsync(v => v.ActiveMs);
            Assert.Equal(61 * Seconds, counted);
        }

        [Fact]
        public async Task OneReport_AddsAMinuteAtMost()
        {
            var (headCoach, player, playbookId) = await TeamWithAPlayerAsync();
            await ReportAsync(player, playbookId, "page-one", 1 * Seconds);

            // Ten minutes later the page claims five minutes in a single report
            _world.Clock.Advance(TimeSpan.FromMinutes(10));
            var credit = await ReportAsync(player, playbookId, "page-one", 5 * 60 * Seconds);

            Assert.Equal(StudyTimeService.MaxPerReportMs, credit.CreditedMs);
        }

        [Fact]
        public async Task FilmAndPlaybookTime_AreKeptApart()
        {
            var (headCoach, player, playbookId) = await TeamWithAPlayerAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int clipId = await _world.AddClipAsync(headCoach, film.VideoId, 10, 18);

            int cutupId;
            using (var setup = _world.NewContext())
            {
                var cutups = _world.Cutups(setup);
                cutupId = (await cutups.CreateAsync(headCoach, new SaveCutupDto { Name = "For the team" })).Id;
                await cutups.AddClipsAsync(headCoach, cutupId, new AddCutupClipsDto { ClipIds = { clipId } });
                await cutups.SetSharingAsync(headCoach, cutupId, new SetCutupSharingDto { WholeTeam = true });
            }

            _world.Clock.Advance(TimeSpan.FromSeconds(15));
            await ReportAsync(player, playbookId, "playbook-page", 15 * Seconds);

            _world.Clock.Advance(TimeSpan.FromSeconds(40));
            using (var context = _world.NewContext())
            {
                await _world.StudyTime(context).ReportAsync(player, new StudyReportDto
                {
                    Kind = ViewingKind.Film,
                    CutupId = cutupId,
                    PageKey = "film-page",
                    ActiveMs = 40 * Seconds
                });
            }

            using var check = _world.NewContext();
            var sessions = await check.ViewingSessions.OrderBy(v => v.Id).ToListAsync();
            Assert.Equal(new[] { ViewingKind.Playbook, ViewingKind.Film }, sessions.Select(v => v.Kind));
            Assert.Equal(new[] { 15 * Seconds, 40 * Seconds }, sessions.Select(v => v.ActiveMs));
            Assert.Equal(cutupId, sessions[1].CutupId);
            Assert.Null(sessions[1].PlaybookId);

            // The film time stays when the cutup is deleted later: the record just stops pointing at it
            using (var context = _world.NewContext())
            {
                await _world.Cutups(context).DeleteAsync(headCoach, cutupId);
            }
            using var afterDelete = _world.NewContext();
            var filmSession = await afterDelete.ViewingSessions.SingleAsync(v => v.Kind == ViewingKind.Film);
            Assert.Null(filmSession.CutupId);
            Assert.Equal(40 * Seconds, filmSession.ActiveMs);
        }

        [Fact]
        public async Task APage_CountsTimeForOneThingOnly()
        {
            var (headCoach, player, offencePlaybook) = await TeamWithAPlayerAsync();
            var twoWayPlayer = await _world.AddPlayerAsync(headCoach, "Kim", "Receivers", "Defensive Backs");
            int defencePlaybook = await _world.PlaybookIdAsync(headCoach, TeamUnit.Defence);

            _world.Clock.Advance(TimeSpan.FromSeconds(15));
            await ReportAsync(twoWayPlayer, offencePlaybook, "page-one", 15 * Seconds);

            // The same page key cannot start counting for another playbook
            _world.Clock.Advance(TimeSpan.FromSeconds(15));
            await Assert.ThrowsAsync<InvalidOperationException>(() => ReportAsync(twoWayPlayer, defencePlaybook, "page-one", 15 * Seconds));
        }

        [Fact]
        public async Task OnlyPlayers_AreCounted()
        {
            var (headCoach, player, playbookId) = await TeamWithAPlayerAsync();
            var positionCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");

            await Assert.ThrowsAsync<ForbiddenException>(() => ReportAsync(headCoach, playbookId, "coach-page", 15 * Seconds));
            await Assert.ThrowsAsync<ForbiddenException>(() => ReportAsync(positionCoach, playbookId, "coach-page", 15 * Seconds));

            using var context = _world.NewContext();
            Assert.Equal(0, await context.ViewingSessions.CountAsync());
        }

        [Fact]
        public async Task TimeIsCountedOnlyOnWhatThePlayerMayOpen()
        {
            var (headCoach, receiver, offencePlaybook) = await TeamWithAPlayerAsync();
            int defencePlaybook = await _world.PlaybookIdAsync(headCoach, TeamUnit.Defence);

            // A cutup that is shared with the linebackers, not with the receivers
            var film = await _world.AddFilmAsync(headCoach);
            int clipId = await _world.AddClipAsync(headCoach, film.VideoId, 10, 18);
            int cutupId;
            using (var setup = _world.NewContext())
            {
                var cutups = _world.Cutups(setup);
                cutupId = (await cutups.CreateAsync(headCoach, new SaveCutupDto { Name = "For the linebackers" })).Id;
                await cutups.AddClipsAsync(headCoach, cutupId, new AddCutupClipsDto { ClipIds = { clipId } });
                await cutups.SetSharingAsync(headCoach, cutupId, new SetCutupSharingDto
                {
                    GroupIds = { await _world.GroupIdAsync(headCoach, "Linebackers") }
                });
            }

            // Another team's playbook
            var outsider = await _world.AddTeamAsync("Other Team");
            int theirPlaybook = await _world.PlaybookIdAsync(outsider, TeamUnit.Offence);

            using var context = _world.NewContext();
            var service = _world.StudyTime(context);

            await Assert.ThrowsAsync<ForbiddenException>(() => ReportAsync(receiver, defencePlaybook, "page-a1", 15 * Seconds));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.ReportAsync(receiver, new StudyReportDto
            {
                Kind = ViewingKind.Film, CutupId = cutupId, PageKey = "page-b2", ActiveMs = 15 * Seconds
            }));
            Assert.Null(await service.ReportAsync(receiver, new StudyReportDto
            {
                Kind = ViewingKind.Playbook, PlaybookId = theirPlaybook, PageKey = "page-c3", ActiveMs = 15 * Seconds
            }));

            Assert.Equal(0, await context.ViewingSessions.CountAsync());
        }
    }
}
