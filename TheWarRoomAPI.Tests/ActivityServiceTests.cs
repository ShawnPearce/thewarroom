using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The activity report: login time, film time and playbook time as three separate numbers, and who may see whose
    public class ActivityServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        // Tests run on Wednesday 2 September 2026 at noon (UTC). Monday of that week:
        private static readonly DateTime ThisMonday = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc);

        public void Dispose()
        {
            _world.Dispose();
        }

        [Fact]
        public async Task TheReport_ShowsLoginFilmAndPlaybookTime_AsThreeSeparateNumbers()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");

            // Tuesday evening: signed in for 40 minutes, of which 20 watching film and 10 reading the playbook
            var tuesdayEvening = ThisMonday.AddDays(1).AddHours(19);
            await _world.AddLoginSessionAsync(player, tuesdayEvening, minutes: 40);
            await _world.AddViewingAsync(player, ViewingKind.Film, tuesdayEvening.AddMinutes(2), minutes: 20);
            await _world.AddViewingAsync(player, ViewingKind.Playbook, tuesdayEvening.AddMinutes(25), minutes: 10);

            using var context = _world.NewContext();
            var report = await _world.Activity(context).GetWeekAsync(headCoach, null);

            Assert.Equal(TestWorld.ThisMonday, report.WeekStart);
            var row = Assert.Single(report.Players);
            Assert.Equal("Walker", row.LastName);
            Assert.Equal(new[] { "Receivers" }, row.Groups);
            Assert.Equal(40 * 60, row.LoginSeconds);
            Assert.Equal(20 * 60, row.FilmSeconds);
            Assert.Equal(10 * 60, row.PlaybookSeconds);
            Assert.Equal(1, row.SignIns);
        }

        [Fact]
        public async Task LoginTime_RunsFromSigningInToTheLastRequest()
        {
            var headCoach = await _world.AddTeamAsync();
            var account = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");

            // A real sign-in, with requests after 10 and after 25 minutes, and nothing after that
            string token;
            using (var context = _world.NewContext())
            {
                var user = await context.Users.SingleAsync(u => u.Id == account.Id);
                token = (await _world.Auth(context).StartSessionAsync(user)).SessionToken;
            }
            foreach (int minutesLater in new[] { 10, 15 })
            {
                _world.Clock.Advance(TimeSpan.FromMinutes(minutesLater));
                using var context = _world.NewContext();
                Assert.NotNull(await _world.Auth(context).ResolveCurrentUserAsync(account.Id, token));
            }
            _world.Clock.Advance(TimeSpan.FromHours(3));   // the browser was left open; nothing was requested

            using var reportContext = _world.NewContext();
            var row = (await _world.Activity(reportContext).GetWeekAsync(headCoach, null)).Players.Single();

            Assert.Equal(25 * 60, row.LoginSeconds);   // not three hours and 25 minutes
            Assert.Equal(0, row.FilmSeconds);
            Assert.Equal(0, row.PlaybookSeconds);
        }

        [Fact]
        public async Task EachWeek_HasItsOwnTotals()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");

            // Last week
            await _world.AddLoginSessionAsync(player, ThisMonday.AddDays(-5).AddHours(18), minutes: 30);
            await _world.AddViewingAsync(player, ViewingKind.Playbook, ThisMonday.AddDays(-5).AddHours(18), minutes: 12);

            // This week: two sign-ins
            await _world.AddLoginSessionAsync(player, ThisMonday.AddHours(18), minutes: 20);
            await _world.AddLoginSessionAsync(player, ThisMonday.AddDays(1).AddHours(18), minutes: 15);
            await _world.AddViewingAsync(player, ViewingKind.Film, ThisMonday.AddHours(18), minutes: 9);

            // Sunday night, one minute before this week began, belongs to last week
            await _world.AddViewingAsync(player, ViewingKind.Film, ThisMonday.AddMinutes(-1), minutes: 3);

            using var context = _world.NewContext();
            var service = _world.Activity(context);

            var history = await service.GetPlayerAsync(headCoach, player.Id, weeks: 3);
            Assert.Equal(new[] { TestWorld.ThisMonday, TestWorld.ThisMonday.AddDays(-7), TestWorld.ThisMonday.AddDays(-14) },
                history!.Weeks.Select(w => w.WeekStart));

            var thisWeek = history.Weeks[0];
            Assert.Equal((35 * 60, 9 * 60, 0, 2), (thisWeek.LoginSeconds, thisWeek.FilmSeconds, thisWeek.PlaybookSeconds, thisWeek.SignIns));

            var lastWeek = history.Weeks[1];
            Assert.Equal((30 * 60, 3 * 60, 12 * 60, 1), (lastWeek.LoginSeconds, lastWeek.FilmSeconds, lastWeek.PlaybookSeconds, lastWeek.SignIns));

            var theWeekBefore = history.Weeks[2];
            Assert.Equal((0, 0, 0, 0), (theWeekBefore.LoginSeconds, theWeekBefore.FilmSeconds, theWeekBefore.PlaybookSeconds, theWeekBefore.SignIns));

            // The team report for a day in last week shows last week's numbers
            var lastWeekReport = await service.GetWeekAsync(headCoach, TestWorld.Today.AddDays(-7));
            Assert.Equal(TestWorld.ThisMonday.AddDays(-7), lastWeekReport.WeekStart);
            Assert.Equal(12 * 60, lastWeekReport.Players.Single().PlaybookSeconds);
        }

        [Fact]
        public async Task AWeekThatHasNotBegun_GivesTheCurrentWeek()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            await _world.AddViewingAsync(player, ViewingKind.Playbook, ThisMonday.AddHours(18), minutes: 5);

            using var context = _world.NewContext();
            var report = await _world.Activity(context).GetWeekAsync(headCoach, TestWorld.Today.AddDays(21));

            Assert.Equal(TestWorld.ThisMonday, report.WeekStart);
            Assert.Equal(5 * 60, report.Players.Single().PlaybookSeconds);
        }

        [Fact]
        public async Task EveryPlayerInScope_IsListed_AlsoThoseWithNoActivity()
        {
            var headCoach = await _world.AddTeamAsync();
            var active = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            await _world.AddPlayerAsync(headCoach, "Kim", "Receivers");
            await _world.AddViewingAsync(active, ViewingKind.Playbook, ThisMonday.AddHours(18), minutes: 5);

            using var context = _world.NewContext();
            var report = await _world.Activity(context).GetWeekAsync(headCoach, null);

            Assert.Equal(2, report.Players.Count);
            var quiet = report.Players.Single(p => p.LastName == "Kim");
            Assert.Equal((0, 0, 0, 0), (quiet.LoginSeconds, quiet.FilmSeconds, quiet.PlaybookSeconds, quiet.SignIns));
        }

        [Fact]
        public async Task ACoach_SeesThePlayersOfTheirGroups_AndTheHeadCoachSeesEveryone()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var defenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Defence);

            var receiver = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var linebacker = await _world.AddPlayerAsync(headCoach, "Ford", "Linebackers");
            var twoWayPlayer = await _world.AddPlayerAsync(headCoach, "Kim", "Receivers", "Defensive Backs");
            var noGroupYet = await _world.AddPlayerAsync(headCoach, "Newcomer");

            using var context = _world.NewContext();
            var service = _world.Activity(context);

            async Task<string[]> SeenByAsync(CurrentUser coach) =>
                (await service.GetWeekAsync(coach, null)).Players.Select(p => p.LastName).Order().ToArray();

            Assert.Equal(new[] { "Ford", "Kim", "Newcomer", "Walker" }, await SeenByAsync(headCoach));
            Assert.Equal(new[] { "Kim", "Walker" }, await SeenByAsync(receiversCoach));
            Assert.Equal(new[] { "Ford", "Kim" }, await SeenByAsync(defenceCoordinator));   // every defensive group

            // Opening one player follows the same rule
            Assert.NotNull(await service.GetPlayerAsync(receiversCoach, twoWayPlayer.Id, 4));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetPlayerAsync(receiversCoach, linebacker.Id, 4));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetPlayerAsync(defenceCoordinator, receiver.Id, 4));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetPlayerAsync(receiversCoach, noGroupYet.Id, 4));
            Assert.NotNull(await service.GetPlayerAsync(headCoach, noGroupYet.Id, 4));
        }

        [Fact]
        public async Task APlayer_SeesTheirOwnActivity_AndNobodyElses()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var teammate = await _world.AddPlayerAsync(headCoach, "Kim", "Receivers");
            await _world.AddViewingAsync(player, ViewingKind.Playbook, ThisMonday.AddHours(18), minutes: 7);

            using var context = _world.NewContext();
            var service = _world.Activity(context);

            var mine = await service.GetPlayerAsync(player, player.Id, 2);
            Assert.Equal(7 * 60, mine!.Weeks[0].PlaybookSeconds);

            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetPlayerAsync(player, teammate.Id, 2));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetWeekAsync(player, null));
        }

        [Fact]
        public async Task ADeactivatedPlayer_IsLeftOutOfTheTeamReport()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            await _world.AddPlayerAsync(headCoach, "Kim", "Receivers");

            using (var setup = _world.NewContext())
            {
                (await setup.Users.SingleAsync(u => u.Id == player.Id)).IsActive = false;
                await setup.SaveChangesAsync();
            }

            using var context = _world.NewContext();
            var report = await _world.Activity(context).GetWeekAsync(headCoach, null);

            Assert.Equal("Kim", Assert.Single(report.Players).LastName);
        }

        [Fact]
        public async Task AnotherTeamsPlayers_CannotBeReached()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            await _world.AddViewingAsync(player, ViewingKind.Playbook, ThisMonday.AddHours(18), minutes: 7);

            var outsider = await _world.AddTeamAsync("Other Team");

            using var context = _world.NewContext();
            var service = _world.Activity(context);

            Assert.Null(await service.GetPlayerAsync(outsider, player.Id, 4));
            Assert.Empty((await service.GetWeekAsync(outsider, null)).Players);

            // A coach has no study time of their own
            Assert.Null(await service.GetPlayerAsync(headCoach, headCoach.Id, 4));
        }
    }
}
