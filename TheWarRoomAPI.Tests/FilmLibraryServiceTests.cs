using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The teams on film and the games between them: what is valid, who may work with them, and keeping teams apart
    public class FilmLibraryServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        private static SaveGameDto NewGame(int homeTeamId, int awayTeamId, DateOnly? date = null)
        {
            return new SaveGameDto
            {
                GameDate = date ?? TestWorld.Today.AddDays(-5),
                HomeTeamId = homeTeamId,
                AwayTeamId = awayTeamId,
                Label = "Week 1"
            };
        }

        // ---------- Teams on film ----------

        [Fact]
        public async Task ANewTeam_HasItsOwnEntryAmongTheTeamsOnFilm()
        {
            var headCoach = await _world.AddTeamAsync("Test Tigers");

            using var context = _world.NewContext();
            var teams = await _world.ScoutedTeams(context).GetTeamsAsync(headCoach);

            var own = Assert.Single(teams);
            Assert.True(own.IsOwnTeam);
            Assert.Equal("Test Tigers", own.Name);
        }

        [Fact]
        public async Task TheTeamsOwnEntry_CannotBeRenamedOrRemovedHere_AndFollowsTheTeamsName()
        {
            var headCoach = await _world.AddTeamAsync("Test Tigers");
            int ownId = await _world.OwnTeamOnFilmIdAsync(headCoach);

            using var context = _world.NewContext();
            var service = _world.ScoutedTeams(context);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateAsync(headCoach, ownId, new SaveScoutedTeamDto { Name = "Something Else" }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(headCoach, ownId));

            // Renaming the team in its settings renames the entry
            await _world.Teams(context).UpdateAsync(headCoach, new UpdateTeamDto { Name = "Test Lions" });

            Assert.Equal("Test Lions", (await service.GetTeamAsync(headCoach, ownId))!.Name);
        }

        [Fact]
        public async Task RenamingTheTeam_ToTheNameOfAnOpponentOnFilm_IsRefused()
        {
            var headCoach = await _world.AddTeamAsync("Test Tigers");
            await _world.AddOpponentAsync(headCoach, "Riverside Raiders");

            using var context = _world.NewContext();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Teams(context).UpdateAsync(headCoach, new UpdateTeamDto { Name = "riverside raiders" }));

            Assert.Contains("already called", error.Message);
        }

        [Fact]
        public async Task TeamNamesOnFilm_AreUnique_WithoutRegardToCase()
        {
            var headCoach = await _world.AddTeamAsync("Test Tigers");

            using var context = _world.NewContext();
            var service = _world.ScoutedTeams(context);

            var raiders = await service.CreateAsync(headCoach, new SaveScoutedTeamDto { Name = "  Riverside Raiders " });
            var knights = await service.CreateAsync(headCoach, new SaveScoutedTeamDto { Name = "Northgate Knights" });
            Assert.Equal("Riverside Raiders", raiders.Name);   // spaces around the name are dropped
            Assert.False(raiders.IsOwnTeam);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(headCoach, new SaveScoutedTeamDto { Name = "RIVERSIDE RAIDERS" }));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(headCoach, new SaveScoutedTeamDto { Name = "test tigers" }));   // the team's own name
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateAsync(headCoach, knights.Id, new SaveScoutedTeamDto { Name = "Riverside Raiders" }));

            // A team can be renamed to its own name in different letters
            Assert.True(await service.UpdateAsync(headCoach, knights.Id, new SaveScoutedTeamDto { Name = "Northgate KNIGHTS" }));
        }

        [Fact]
        public async Task AnOpponentWithGamesOnFilm_CannotBeRemoved_ButOneWithoutCan()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach, "Riverside Raiders");
            int unused = await _world.AddOpponentAsync(headCoach, "Northgate Knights");

            using var context = _world.NewContext();
            var service = _world.ScoutedTeams(context);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(headCoach, film.OpponentId));
            Assert.Contains("games on film", error.Message);

            Assert.True(await service.DeleteAsync(headCoach, unused));
            Assert.Equal(1, (await service.GetTeamAsync(headCoach, film.OpponentId))!.GameCount);
        }

        // ---------- Games ----------

        [Fact]
        public async Task AGame_NeedsTwoDifferentTeams_FromThisTeamsFilm()
        {
            var headCoach = await _world.AddTeamAsync();
            int ownId = await _world.OwnTeamOnFilmIdAsync(headCoach);

            var otherHeadCoach = await _world.AddTeamAsync("Other Team");
            int theirOpponent = await _world.AddOpponentAsync(otherHeadCoach, "Somewhere Else");

            using var context = _world.NewContext();
            var service = _world.Games(context);

            var sameTeamTwice = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(headCoach, NewGame(ownId, ownId)));
            Assert.Contains("two different teams", sameTeamTwice.Message);

            // A team that belongs to another workspace's film cannot be used
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(headCoach, NewGame(ownId, theirOpponent)));
        }

        [Fact]
        public async Task AGame_CanBeBetweenTwoOpponents_AndAnyCoachCanAddIt()
        {
            var headCoach = await _world.AddTeamAsync();
            var positionCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Linebackers");
            int knights = await _world.AddOpponentAsync(headCoach, "Northgate Knights");
            int raiders = await _world.AddOpponentAsync(headCoach, "Riverside Raiders");

            using var context = _world.NewContext();
            var game = await _world.Games(context).CreateAsync(positionCoach, NewGame(knights, raiders));

            Assert.Equal("Northgate Knights", game.HomeTeamName);
            Assert.Equal("Riverside Raiders", game.AwayTeamName);
            Assert.Equal(0, game.VideoCount);
        }

        [Fact]
        public async Task Games_AreListedNewestFirst_AndCanBeNarrowedToOneTeam()
        {
            var headCoach = await _world.AddTeamAsync();
            int ownId = await _world.OwnTeamOnFilmIdAsync(headCoach);
            int knights = await _world.AddOpponentAsync(headCoach, "Northgate Knights");
            int raiders = await _world.AddOpponentAsync(headCoach, "Riverside Raiders");

            int older = await _world.AddGameAsync(headCoach, ownId, raiders, TestWorld.Today.AddDays(-12));
            int newer = await _world.AddGameAsync(headCoach, knights, raiders, TestWorld.Today.AddDays(-5));

            using var context = _world.NewContext();
            var service = _world.Games(context);

            Assert.Equal(new[] { newer, older }, (await service.GetGamesAsync(headCoach, null)).Select(g => g.Id));
            Assert.Equal(new[] { newer }, (await service.GetGamesAsync(headCoach, knights)).Select(g => g.Id));
            Assert.Equal(2, (await service.GetGamesAsync(headCoach, raiders)).Count);
        }

        [Fact]
        public async Task AGame_IsReturnedWithItsVideos_AndCountsOfItsClips()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            await _world.AddClipAsync(headCoach, film.VideoId, 10, 18, film.OwnTeamId);
            await _world.AddClipAsync(headCoach, film.VideoId, 40, 47);   // not tagged

            using var context = _world.NewContext();
            var game = await _world.Games(context).GetGameAsync(headCoach, film.GameId);

            Assert.Equal(2, game!.ClipCount);
            Assert.Equal(1, game.TaggedClipCount);

            var video = Assert.Single(game.Videos);
            Assert.Equal(VideoStatus.Ready, video.Status);
            Assert.Equal(TestWorld.OneHourMs, video.DurationMs);
            Assert.False(video.HasFile);   // the record exists, but there is no file in storage
            Assert.Equal(2, video.ClipCount);
        }

        [Fact]
        public async Task AGameWithVideo_CannotBeDeleted_ButOneWithoutCan()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int empty = await _world.AddGameAsync(headCoach, film.OwnTeamId, film.OpponentId, TestWorld.Today.AddDays(-12));

            using var context = _world.NewContext();
            var service = _world.Games(context);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(headCoach, film.GameId));
            Assert.Contains("has video", error.Message);

            Assert.True(await service.DeleteAsync(headCoach, empty));
        }

        [Fact]
        public async Task ATeamThatIsOnOffenceInATaggedPlay_HasToStayInItsGame()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach, "Riverside Raiders");
            int knights = await _world.AddOpponentAsync(headCoach, "Northgate Knights");
            await _world.AddClipAsync(headCoach, film.VideoId, 10, 18, film.OpponentId);   // the Raiders have the ball

            using var context = _world.NewContext();
            var service = _world.Games(context);

            // Swapping the Raiders for the Knights would leave that play pointing at a team that did not play
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateAsync(headCoach, film.GameId, NewGame(film.OwnTeamId, knights)));
            Assert.Contains("Riverside Raiders", error.Message);

            // Swapping which side is at home is fine
            Assert.True(await service.UpdateAsync(headCoach, film.GameId, NewGame(film.OpponentId, film.OwnTeamId)));
        }

        // ---------- Who may open the film library ----------

        [Fact]
        public async Task Players_CannotOpenGameFilm()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();

            await Assert.ThrowsAsync<ForbiddenException>(() => _world.ScoutedTeams(context).GetTeamsAsync(player));
            await Assert.ThrowsAsync<ForbiddenException>(() => _world.Games(context).GetGamesAsync(player, null));
            await Assert.ThrowsAsync<ForbiddenException>(() => _world.Games(context).GetGameAsync(player, film.GameId));
            await Assert.ThrowsAsync<ForbiddenException>(() => _world.Videos(context).GetVideoAsync(player, film.VideoId));
            await Assert.ThrowsAsync<ForbiddenException>(() => _world.Clips(context).GetClipsAsync(player, new ClipFilterDto()));
            await Assert.ThrowsAsync<ForbiddenException>(
                () => _world.Games(context).CreateAsync(player, NewGame(film.OwnTeamId, film.OpponentId)));
        }

        [Fact]
        public async Task AnotherTeamsFilm_CannotBeReached()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            var outsider = await _world.AddTeamAsync("Other Team");

            using var context = _world.NewContext();
            var teams = _world.ScoutedTeams(context);
            var games = _world.Games(context);

            Assert.Null(await teams.GetTeamAsync(outsider, film.OpponentId));
            Assert.False(await teams.UpdateAsync(outsider, film.OpponentId, new SaveScoutedTeamDto { Name = "Renamed" }));
            Assert.False(await teams.DeleteAsync(outsider, film.OpponentId));

            Assert.Null(await games.GetGameAsync(outsider, film.GameId));
            Assert.False(await games.DeleteAsync(outsider, film.GameId));
            Assert.Empty(await games.GetGamesAsync(outsider, null));

            Assert.Null(await _world.Videos(context).GetVideoAsync(outsider, film.VideoId));

            // Nothing was changed
            using var check = _world.NewContext();
            Assert.Equal("Riverside Raiders", await check.ScoutedTeams.Where(t => t.Id == film.OpponentId).Select(t => t.Name).SingleAsync());
        }
    }
}
