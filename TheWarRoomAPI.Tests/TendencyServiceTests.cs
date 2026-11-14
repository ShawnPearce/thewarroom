using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Tendencies counted from tagged film: which plays a filter picks, what is left out, and who may see them
    public class TendencyServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        private async Task<TendencyReportDto> ReportAsync(CurrentUser coach, TendencyFilterDto filter)
        {
            using var context = _world.NewContext();
            return await _world.Tendencies(context).GetReportAsync(coach, filter);
        }

        [Fact]
        public async Task TheChosenTeamAndDown_GiveTheProposalsExample()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach, "Northgate Knights");

            // Northgate on first down: 12 runs and 8 passes
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OpponentId, down: 1, distance: 10, runs: 12, passes: 8);

            // Plays that must not be counted: another down, and the other team with the ball
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OpponentId, down: 3, distance: 8, runs: 1, passes: 6);
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OwnTeamId, down: 1, distance: 10, runs: 2, passes: 9);

            var report = await ReportAsync(headCoach, new TendencyFilterDto { OffenceTeamId = film.OpponentId, Down = 1 });

            Assert.Equal(12, report.Selected.Runs);
            Assert.Equal(8, report.Selected.Passes);
            Assert.Equal(20, report.Selected.Plays);
            Assert.Equal(60.0m, report.Selected.RunPercent);
            Assert.Equal(40.0m, report.Selected.PassPercent);

            Assert.Equal(27, report.TaggedPlays);   // all of Northgate's tagged plays in these games
            Assert.Equal(1, report.Games);
        }

        [Fact]
        public async Task ASituationThatNeverHappened_IsNoData_NotZeroPercent()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OpponentId, down: 1, distance: 10, runs: 3, passes: 3);

            var report = await ReportAsync(headCoach, new TendencyFilterDto { OffenceTeamId = film.OpponentId, Down = 4 });

            Assert.Equal(0, report.Selected.Plays);
            Assert.Null(report.Selected.RunPercent);
            Assert.Null(report.Selected.PassPercent);
        }

        [Fact]
        public async Task PlaysWithoutATag_AreNotCounted()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OpponentId, down: 2, distance: 5, runs: 1, passes: 1);
            await _world.AddClipAsync(headCoach, film.VideoId, 3000, 3010);   // a punt: cut, but neither a run nor a pass

            var report = await ReportAsync(headCoach, new TendencyFilterDto());

            Assert.Equal(2, report.TaggedPlays);
            Assert.Equal(2, report.Selected.Plays);
        }

        [Fact]
        public async Task TheGamesFilter_CountsOneGame_SeveralGames_OrAllOfThem()
        {
            var headCoach = await _world.AddTeamAsync();
            var first = await _world.AddFilmAsync(headCoach, "Northgate Knights");

            // Two more games of the same opponent
            int secondGame = await _world.AddGameAsync(headCoach, first.OpponentId, first.OwnTeamId, TestWorld.Today.AddDays(-12));
            int secondVideo = await _world.AddVideoAsync(headCoach, secondGame);
            int thirdGame = await _world.AddGameAsync(headCoach, first.OwnTeamId, first.OpponentId, TestWorld.Today.AddDays(-19));
            int thirdVideo = await _world.AddVideoAsync(headCoach, thirdGame);

            await _world.AddTaggedPlaysAsync(headCoach, first.VideoId, first.OpponentId, 1, 10, runs: 4, passes: 0);
            await _world.AddTaggedPlaysAsync(headCoach, secondVideo, first.OpponentId, 1, 10, runs: 1, passes: 3);
            await _world.AddTaggedPlaysAsync(headCoach, thirdVideo, first.OpponentId, 1, 10, runs: 0, passes: 2);

            var oneGame = await ReportAsync(headCoach, new TendencyFilterDto { OffenceTeamId = first.OpponentId, GameIds = { first.GameId } });
            var twoGames = await ReportAsync(headCoach, new TendencyFilterDto { OffenceTeamId = first.OpponentId, GameIds = { first.GameId, secondGame } });
            var everyGame = await ReportAsync(headCoach, new TendencyFilterDto { OffenceTeamId = first.OpponentId });

            Assert.Equal((4, 100.0m), (oneGame.Selected.Plays, oneGame.Selected.RunPercent!.Value));
            Assert.Equal((8, 62.5m), (twoGames.Selected.Plays, twoGames.Selected.RunPercent!.Value));
            Assert.Equal((10, 50.0m), (everyGame.Selected.Plays, everyGame.Selected.RunPercent!.Value));

            Assert.Equal(1, oneGame.Games);
            Assert.Equal(2, twoGames.Games);
            Assert.Equal(3, everyGame.Games);
        }

        [Fact]
        public async Task TheDistanceFilter_UsesTheRangeTheYardsFallIn()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int team = film.OpponentId;

            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, team, down: 3, distance: 1, runs: 3, passes: 0);    // short
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, team, down: 3, distance: 3, runs: 1, passes: 1);    // short
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, team, down: 3, distance: 4, runs: 1, passes: 2);    // medium
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, team, down: 3, distance: 10, runs: 0, passes: 4);   // long
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, team, down: 3, distance: 11, runs: 0, passes: 3);   // very long

            async Task<(int Runs, int Passes)> CountAsync(DistanceRange range)
            {
                var report = await ReportAsync(headCoach, new TendencyFilterDto { OffenceTeamId = team, Down = 3, Distance = range });
                return (report.Selected.Runs, report.Selected.Passes);
            }

            Assert.Equal((4, 1), await CountAsync(DistanceRange.Short));
            Assert.Equal((1, 2), await CountAsync(DistanceRange.Medium));
            Assert.Equal((0, 4), await CountAsync(DistanceRange.Long));
            Assert.Equal((0, 3), await CountAsync(DistanceRange.VeryLong));
        }

        [Fact]
        public async Task TheBreakdowns_ShowTheChosenSituationInContext()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int team = film.OpponentId;

            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, team, down: 1, distance: 10, runs: 6, passes: 4);
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, team, down: 2, distance: 2, runs: 3, passes: 0);
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, team, down: 3, distance: 2, runs: 2, passes: 1);
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, team, down: 3, distance: 8, runs: 1, passes: 5);

            // Third and long
            var report = await ReportAsync(headCoach, new TendencyFilterDto { OffenceTeamId = team, Down = 3, Distance = DistanceRange.Long });

            Assert.Equal((1, 5), (report.Selected.Runs, report.Selected.Passes));
            Assert.Equal(16.7m, report.Selected.RunPercent);

            // By down keeps the distance: the long-yardage plays on each down
            Assert.Equal(new[] { 1, 2, 3, 4 }, report.ByDown.Select(s => s.Down!.Value));
            Assert.Equal(new[] { 10, 0, 6, 0 }, report.ByDown.Select(s => s.Plays));

            // By distance keeps the down: every distance on third down
            Assert.Equal(TendencyCalculator.Ranges, report.ByDistance.Select(s => s.Distance!.Value));
            Assert.Equal(new[] { 3, 0, 6, 0 }, report.ByDistance.Select(s => s.Plays));

            // The grid is every down against every distance, whatever down and distance were chosen
            Assert.Equal(4, report.Grid.Count);
            Assert.All(report.Grid, row => Assert.Equal(4, row.Cells.Count));
            var secondAndShort = report.Grid[1].Cells[0];
            Assert.Equal((2, DistanceRange.Short, 3, 100.0m), (secondAndShort.Down!.Value, secondAndShort.Distance!.Value, secondAndShort.Plays, secondAndShort.RunPercent!.Value));
            Assert.Null(report.Grid[3].Cells[3].RunPercent);   // fourth and very long never happened

            // Every tagged play is in exactly one cell
            Assert.Equal(report.TaggedPlays, report.Grid.Sum(row => row.Cells.Sum(cell => cell.Plays)));
        }

        [Fact]
        public async Task WithoutATeam_EveryTaggedPlayOfTheChosenGamesIsCounted()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OpponentId, 1, 10, runs: 2, passes: 1);
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OwnTeamId, 1, 10, runs: 1, passes: 4);

            var report = await ReportAsync(headCoach, new TendencyFilterDto { GameIds = { film.GameId } });

            Assert.Equal((3, 5), (report.Selected.Runs, report.Selected.Passes));
        }

        [Fact]
        public async Task Players_DoNotSeeTendencies()
        {
            var headCoach = await _world.AddTeamAsync();
            var positionCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Linebackers");
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");

            using var context = _world.NewContext();
            var service = _world.Tendencies(context);

            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetReportAsync(player, new TendencyFilterDto()));

            // Every coach does
            Assert.NotNull(await service.GetReportAsync(positionCoach, new TendencyFilterDto()));
        }

        [Fact]
        public async Task AnotherTeamsFilm_IsNeverCounted()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach, "Northgate Knights");
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OpponentId, 1, 10, runs: 5, passes: 5);

            var outsider = await _world.AddTeamAsync("Other Team");
            var theirFilm = await _world.AddFilmAsync(outsider, "Northgate Knights");   // they scout the same opponent
            await _world.AddTaggedPlaysAsync(outsider, theirFilm.VideoId, theirFilm.OpponentId, 1, 10, runs: 1, passes: 0);

            // Each team sees only what it tagged itself
            Assert.Equal(1, (await ReportAsync(outsider, new TendencyFilterDto())).TaggedPlays);
            Assert.Equal(10, (await ReportAsync(headCoach, new TendencyFilterDto())).TaggedPlays);

            // Asking for the other team's game or its team on film gives nothing
            Assert.Equal(0, (await ReportAsync(outsider, new TendencyFilterDto { GameIds = { film.GameId } })).TaggedPlays);
            Assert.Equal(0, (await ReportAsync(outsider, new TendencyFilterDto { OffenceTeamId = film.OpponentId })).Selected.Plays);
        }

        [Fact]
        public async Task TheClipsBehindAFigure_CanBeListedWithTheSameFilters()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int secondGame = await _world.AddGameAsync(headCoach, film.OwnTeamId, film.OpponentId, TestWorld.Today.AddDays(-12));
            int secondVideo = await _world.AddVideoAsync(headCoach, secondGame);
            int thirdGame = await _world.AddGameAsync(headCoach, film.OwnTeamId, film.OpponentId, TestWorld.Today.AddDays(-19));
            int thirdVideo = await _world.AddVideoAsync(headCoach, thirdGame);

            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OpponentId, down: 3, distance: 8, runs: 1, passes: 2);
            await _world.AddTaggedPlaysAsync(headCoach, secondVideo, film.OpponentId, down: 3, distance: 7, runs: 0, passes: 3);
            await _world.AddTaggedPlaysAsync(headCoach, thirdVideo, film.OpponentId, down: 3, distance: 9, runs: 4, passes: 0);   // a game that is not chosen
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OpponentId, down: 3, distance: 11, runs: 0, passes: 1);  // very long, not long

            var filter = new TendencyFilterDto
            {
                OffenceTeamId = film.OpponentId,
                GameIds = { film.GameId, secondGame },
                Down = 3,
                Distance = DistanceRange.Long
            };
            var report = await ReportAsync(headCoach, filter);

            using var context = _world.NewContext();
            var clips = await _world.Clips(context).GetClipsAsync(headCoach, new ClipFilterDto
            {
                OffenceTeamId = filter.OffenceTeamId,
                GameIds = filter.GameIds,
                Down = filter.Down,
                Distance = filter.Distance
            });

            // The list is exactly the plays the figure was counted from
            Assert.Equal(6, report.Selected.Plays);
            Assert.Equal(report.Selected.Plays, clips.Count);
            Assert.Equal(report.Selected.Runs, clips.Count(c => c.Tag!.PlayType == PlayType.Run));
            Assert.All(clips, c => Assert.InRange(c.Tag!.Distance, 7, 10));
        }

        [Fact]
        public async Task TheDemoData_ContainsTheProposalsExample()
        {
            // The demo team scouts Northgate Knights: on first down they ran 12 times and passed 8 times
            using var database = new TestDatabase();
            using (var seedContext = database.CreateContext())
            {
                DbSeeder.Seed(seedContext, "Demo-Password-1", TimeProvider.System);
            }

            using var context = database.CreateContext();
            var headCoachAccount = await context.Users.FirstAsync(u => u.Role == UserRole.HeadCoach && u.Team!.Name == "Lakeshore Lions");
            var headCoach = await new AuthService(context, new PasswordHasher<User>(), TimeProvider.System)
                .ResolveCurrentUserAsync(headCoachAccount.Id, null);
            int knights = await context.ScoutedTeams
                .Where(t => t.TeamId == headCoachAccount.TeamId && t.Name == "Northgate Knights")
                .Select(t => t.Id)
                .SingleAsync();

            var report = await new TendencyService(context).GetReportAsync(headCoach!, new TendencyFilterDto { OffenceTeamId = knights, Down = 1 });

            Assert.Equal(60.0m, report.Selected.RunPercent);
            Assert.Equal(20, report.Selected.Plays);
            Assert.Equal(2, report.Games);
        }
    }
}
