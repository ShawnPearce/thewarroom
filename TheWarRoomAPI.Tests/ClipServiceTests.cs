using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Clips and their tags: a clip must fit inside its video, a tag must make sense for the game,
    // and a clip that is used in a cutup stays
    public class ClipServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        private static SaveClipDto NewClip(int videoId, int startMs, int endMs, SavePlayTagDto? tag = null)
        {
            return new SaveClipDto { VideoId = videoId, StartMs = startMs, EndMs = endMs, Tag = tag };
        }

        private static SavePlayTagDto Tag(int offenceTeamId, PlayType playType = PlayType.Run, int down = 1, int distance = 10)
        {
            return new SavePlayTagDto { OffenceTeamId = offenceTeamId, PlayType = playType, Down = down, Distance = distance };
        }

        // ---------- A clip lies inside its video ----------

        [Fact]
        public async Task AClip_IsCreatedInsideItsVideo_WithItsTag()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach, "Riverside Raiders");

            using var context = _world.NewContext();
            var dto = NewClip(film.VideoId, 61_000, 69_500, Tag(film.OpponentId, PlayType.Pass, 3, 7));
            dto.Title = "  Screen to the back ";

            var clip = await _world.Clips(context).CreateAsync(headCoach, dto);

            Assert.Equal(61_000, clip!.StartMs);
            Assert.Equal(69_500, clip.EndMs);
            Assert.Equal("Screen to the back", clip.Title);
            Assert.Equal("Riverside Raiders at Test Tigers", clip.Matchup);
            Assert.Equal(PlayType.Pass, clip.Tag!.PlayType);
            Assert.Equal(3, clip.Tag.Down);
            Assert.Equal(7, clip.Tag.Distance);
            Assert.Equal("Riverside Raiders", clip.Tag.OffenceTeamName);
            Assert.Equal(0, clip.CutupCount);
        }

        [Theory]
        [InlineData(-1, 5_000, "before the beginning")]              // starts before the video does
        [InlineData(5_000, 5_000, "after its start")]                // no length
        [InlineData(8_000, 5_000, "after its start")]                // ends before it starts
        [InlineData(0, TestWorld.OneHourMs + 1, "after the end")]    // runs past the end of the video
        [InlineData(TestWorld.OneHourMs + 5_000, TestWorld.OneHourMs + 9_000, "after the end")]
        public async Task AClipThatDoesNotFitItsVideo_IsRejected(int startMs, int endMs, string expectedMessage)
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Clips(context).CreateAsync(headCoach, NewClip(film.VideoId, startMs, endMs)));

            Assert.Contains(expectedMessage, error.Message);
        }

        [Fact]
        public async Task AClip_MayStartAtTheBeginning_AndEndExactlyAtTheEnd()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();
            var service = _world.Clips(context);

            Assert.NotNull(await service.CreateAsync(headCoach, NewClip(film.VideoId, 0, 8_000)));
            Assert.NotNull(await service.CreateAsync(headCoach, NewClip(film.VideoId, TestWorld.OneHourMs - 8_000, TestWorld.OneHourMs)));
        }

        [Theory]
        [InlineData(VideoStatus.Uploading)]
        [InlineData(VideoStatus.Processing)]
        [InlineData(VideoStatus.Failed)]
        public async Task AVideoThatIsNotReady_CannotBeCutIntoPlays(VideoStatus status)
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int unfinished = await _world.AddVideoAsync(headCoach, film.GameId, status, durationMs: null);

            using var context = _world.NewContext();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Clips(context).CreateAsync(headCoach, NewClip(unfinished, 1_000, 9_000)));

            Assert.Contains("not ready", error.Message);
        }

        [Fact]
        public async Task Update_IsCheckedAgainstTheVideoToo()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int clipId = await _world.AddClipAsync(headCoach, film.VideoId, 10, 18);

            using var context = _world.NewContext();
            var service = _world.Clips(context);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateAsync(headCoach, clipId, NewClip(film.VideoId, 20_000, 12_000)));

            // Trimming it to a valid range works
            Assert.True(await service.UpdateAsync(headCoach, clipId, NewClip(film.VideoId, 11_000, 17_500)));
            var clip = await service.GetClipAsync(headCoach, clipId);
            Assert.Equal(11_000, clip!.StartMs);
            Assert.Equal(17_500, clip.EndMs);
        }

        // ---------- Tags ----------

        [Fact]
        public async Task TheTeamOnOffence_MustBeOneOfTheTwoTeamsInTheGame()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach, "Riverside Raiders");
            int knights = await _world.AddOpponentAsync(headCoach, "Northgate Knights");   // did not play in this game

            using var context = _world.NewContext();
            var service = _world.Clips(context);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateAsync(headCoach, NewClip(film.VideoId, 1_000, 9_000, Tag(knights))));
            Assert.Contains("one of the two teams", error.Message);

            // Either team that did play is fine
            Assert.NotNull(await service.CreateAsync(headCoach, NewClip(film.VideoId, 1_000, 9_000, Tag(film.OwnTeamId))));
            Assert.NotNull(await service.CreateAsync(headCoach, NewClip(film.VideoId, 30_000, 39_000, Tag(film.OpponentId))));
        }

        [Theory]
        [InlineData(0, 10)]    // there is no down 0
        [InlineData(5, 10)]    // or down 5
        [InlineData(1, 0)]     // at least a yard to go
        [InlineData(1, 100)]   // and less than the length of the field
        public async Task ATag_NeedsARealDownAndDistance(int down, int distance)
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _world.Clips(context).CreateAsync(
                headCoach, NewClip(film.VideoId, 1_000, 9_000, Tag(film.OwnTeamId, PlayType.Run, down, distance))));
        }

        [Fact]
        public async Task Update_CanAddChangeAndRemoveTheTag()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int clipId = await _world.AddClipAsync(headCoach, film.VideoId, 10, 18);   // not tagged yet

            using var context = _world.NewContext();
            var service = _world.Clips(context);

            await service.UpdateAsync(headCoach, clipId, NewClip(film.VideoId, 10_000, 18_000, Tag(film.OwnTeamId, PlayType.Run, 2, 4)));
            Assert.Equal(PlayType.Run, (await service.GetClipAsync(headCoach, clipId))!.Tag!.PlayType);

            await service.UpdateAsync(headCoach, clipId, NewClip(film.VideoId, 10_000, 18_000, Tag(film.OpponentId, PlayType.Pass, 3, 9)));
            var changed = (await service.GetClipAsync(headCoach, clipId))!.Tag!;
            Assert.Equal(PlayType.Pass, changed.PlayType);
            Assert.Equal(3, changed.Down);
            Assert.Equal(9, changed.Distance);
            Assert.Equal(film.OpponentId, changed.OffenceTeamId);

            await service.UpdateAsync(headCoach, clipId, NewClip(film.VideoId, 10_000, 18_000));
            Assert.Null((await service.GetClipAsync(headCoach, clipId))!.Tag);

            using var check = _world.NewContext();
            Assert.Equal(0, await check.PlayTags.CountAsync());   // the tag itself is gone, not just hidden
        }

        // ---------- Listing ----------

        [Fact]
        public async Task Clips_AreListedInTheOrderTheyHappen_AndNarrowedByTheirTags()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach, "Riverside Raiders");

            // Written out of order on purpose
            int thirdAndLongPass = await _world.AddClipAsync(headCoach, film.VideoId, 300, 309, film.OpponentId, PlayType.Pass, 3, 12);
            int firstDownRun = await _world.AddClipAsync(headCoach, film.VideoId, 60, 68, film.OpponentId, PlayType.Run, 1, 10);
            int ourRun = await _world.AddClipAsync(headCoach, film.VideoId, 120, 127, film.OwnTeamId, PlayType.Run, 2, 3);
            int untagged = await _world.AddClipAsync(headCoach, film.VideoId, 200, 206);

            using var context = _world.NewContext();
            var service = _world.Clips(context);

            async Task<int[]> Ids(ClipFilterDto filter) => (await service.GetClipsAsync(headCoach, filter)).Select(c => c.Id).ToArray();

            Assert.Equal(new[] { firstDownRun, ourRun, untagged, thirdAndLongPass }, await Ids(new ClipFilterDto { VideoId = film.VideoId }));
            Assert.Equal(new[] { firstDownRun, thirdAndLongPass }, await Ids(new ClipFilterDto { OffenceTeamId = film.OpponentId }));
            Assert.Equal(new[] { firstDownRun, ourRun }, await Ids(new ClipFilterDto { PlayType = PlayType.Run }));
            Assert.Equal(new[] { thirdAndLongPass }, await Ids(new ClipFilterDto { Down = 3 }));
            Assert.Equal(new[] { firstDownRun, thirdAndLongPass }, await Ids(new ClipFilterDto { MinDistance = 7 }));
            Assert.Equal(new[] { ourRun }, await Ids(new ClipFilterDto { MaxDistance = 3 }));
            Assert.Equal(new[] { untagged }, await Ids(new ClipFilterDto { Tagged = false }));
            Assert.Equal(3, (await Ids(new ClipFilterDto { GameId = film.GameId, Tagged = true })).Length);
        }

        [Fact]
        public async Task AVideo_IsReturnedWithItsGameAndItsClips()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach, "Riverside Raiders");
            await _world.AddClipAsync(headCoach, film.VideoId, 90, 99);
            await _world.AddClipAsync(headCoach, film.VideoId, 30, 38, film.OwnTeamId);

            using var context = _world.NewContext();
            var video = await _world.Videos(context).GetVideoAsync(headCoach, film.VideoId);

            Assert.Equal("Riverside Raiders", video!.AwayTeamName);
            Assert.Equal("Test Tigers", video.HomeTeamName);
            Assert.Equal(new[] { 30_000, 90_000 }, video.Clips.Select(c => c.StartMs));
            Assert.Equal(1, video.TaggedClipCount);
        }

        // ---------- Clips and cutups ----------

        [Fact]
        public async Task AClipUsedInACutup_CannotBeDeleted_UntilItIsTakenOut()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int clipId = await _world.AddClipAsync(headCoach, film.VideoId, 10, 18, film.OwnTeamId);

            using var context = _world.NewContext();
            var clips = _world.Clips(context);
            var cutups = _world.Cutups(context);

            var cutup = await cutups.CreateAsync(headCoach, new SaveCutupDto { Name = "Red zone" });
            await cutups.AddClipsAsync(headCoach, cutup.Id, new AddCutupClipsDto { ClipIds = { clipId } });
            Assert.Equal(1, (await clips.GetClipAsync(headCoach, clipId))!.CutupCount);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => clips.DeleteAsync(headCoach, clipId));
            Assert.Contains("Red zone", error.Message);

            // Once the cutup no longer uses it, the clip can go, and its tag goes with it
            int playId = (await cutups.GetCutupAsync(headCoach, cutup.Id))!.Plays.Single().Id;
            await cutups.RemovePlayAsync(headCoach, playId);
            Assert.True(await clips.DeleteAsync(headCoach, clipId));

            using var check = _world.NewContext();
            Assert.Equal(0, await check.Clips.CountAsync());
            Assert.Equal(0, await check.PlayTags.CountAsync());
        }

        // ---------- Keeping teams apart ----------

        [Fact]
        public async Task AnotherTeamsVideoAndClips_CannotBeReached()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int clipId = await _world.AddClipAsync(headCoach, film.VideoId, 10, 18);
            var outsider = await _world.AddTeamAsync("Other Team");

            using var context = _world.NewContext();
            var service = _world.Clips(context);

            // The video id in the request belongs to the other team, so there is nothing to cut
            Assert.Null(await service.CreateAsync(outsider, NewClip(film.VideoId, 1_000, 9_000)));

            Assert.Null(await service.GetClipAsync(outsider, clipId));
            Assert.False(await service.UpdateAsync(outsider, clipId, NewClip(film.VideoId, 1_000, 9_000)));
            Assert.False(await service.DeleteAsync(outsider, clipId));
            Assert.Empty(await service.GetClipsAsync(outsider, new ClipFilterDto { VideoId = film.VideoId }));
        }
    }
}
