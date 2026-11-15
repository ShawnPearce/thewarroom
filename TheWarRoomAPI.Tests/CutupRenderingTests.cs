using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Making a cutup into one video: asking for it, the queue, the making itself, and what may happen around it.
    // ffmpeg is stood in for here; FilmRenderingWithFfmpegTests does the same with the real programs.
    public class CutupRenderingTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        // What these tests start from: a video with a file, and three plays cut from it
        private record Film(CurrentUser Coach, int GameId, int VideoId, string VideoKey, int[] ClipIds);

        private async Task<Film> AddFilmWithPlaysAsync()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int videoId = await _world.AddVideoWithFileAsync(headCoach, film.GameId);

            using var context = _world.NewContext();
            var clips = _world.Clips(context);

            // Lengths of 6.5, 4 and 8.25 seconds, at 1:40, 0:05 and 0:21 in the video
            var first = await clips.CreateAsync(headCoach, new SaveClipDto { VideoId = videoId, StartMs = 100_000, EndMs = 106_500, Title = "Sweep left" });
            var second = await clips.CreateAsync(headCoach, new SaveClipDto { VideoId = videoId, StartMs = 5_250, EndMs = 9_250 });
            var third = await clips.CreateAsync(headCoach, new SaveClipDto { VideoId = videoId, StartMs = 21_100, EndMs = 29_350 });

            string key = await context.Videos.Where(v => v.Id == videoId).Select(v => v.StorageKey!).SingleAsync();
            return new Film(headCoach, film.GameId, videoId, key, new[] { first!.Id, second!.Id, third!.Id });
        }

        private async Task<Cutup> CutupAsync(int id)
        {
            using var context = _world.NewContext();
            return await context.Cutups.Include(c => c.Clips).SingleAsync(c => c.Id == id);
        }

        private async Task AskForVideoAsync(CurrentUser coach, int cutupId)
        {
            using var context = _world.NewContext();
            Assert.True(await _world.Cutups(context).RequestRenderAsync(coach, cutupId));
        }

        private async Task<bool> MakeNextAsync()
        {
            using var context = _world.NewContext();
            return await _world.Renderer(context).RenderNextAsync(CancellationToken.None);
        }

        private async Task SetStatusAsync(int cutupId, CutupStatus status)
        {
            using var context = _world.NewContext();
            await context.Cutups.Where(c => c.Id == cutupId).ExecuteUpdateAsync(u => u.SetProperty(c => c.Status, status));
        }

        // ---------- Asking for the video ----------

        [Fact]
        public async Task AskingForTheVideo_PutsTheCutupInTheQueue_AndFixesItsPlays()
        {
            var film = await AddFilmWithPlaysAsync();
            int cutupId = await _world.AddCutupAsync(film.Coach, "Third down", film.ClipIds);

            await AskForVideoAsync(film.Coach, cutupId);

            var cutup = await CutupAsync(cutupId);
            Assert.Equal(CutupStatus.Queued, cutup.Status);
            Assert.Equal(_world.Clock.GetUtcNow().UtcDateTime, cutup.QueuedAt);
            Assert.Null(cutup.StorageKey);

            using var context = _world.NewContext();
            var cutups = _world.Cutups(context);
            int playId = cutup.Clips!.First().Id;

            // Business rule: the plays of a cutup can be changed only while it is a draft
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => cutups.AddClipsAsync(film.Coach, cutupId, new AddCutupClipsDto { ClipIds = film.ClipIds.ToList() }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => cutups.MovePlayAsync(film.Coach, playId, MoveDirection.Down));
            await Assert.ThrowsAsync<InvalidOperationException>(() => cutups.RemovePlayAsync(film.Coach, playId));

            // Asking twice does not put it in the queue twice
            await Assert.ThrowsAsync<InvalidOperationException>(() => cutups.RequestRenderAsync(film.Coach, cutupId));
        }

        [Fact]
        public async Task ACutupWithoutPlays_CannotBeMadeIntoAVideo()
        {
            var film = await AddFilmWithPlaysAsync();
            int cutupId = await _world.AddCutupAsync(film.Coach, "Empty");

            using var context = _world.NewContext();
            var cutups = _world.Cutups(context);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => cutups.RequestRenderAsync(film.Coach, cutupId));
            Assert.Contains("Add plays", error.Message);
            Assert.Equal(error.Message, (await cutups.GetCutupAsync(film.Coach, cutupId))!.RenderProblem);
        }

        [Fact]
        public async Task APlayFromAVideoWithoutAFile_StopsTheVideoBeingMade()
        {
            var film = await AddFilmWithPlaysAsync();

            // A record without a file, like the demo data: plays can be cut and tagged, but there is no film to make a video of
            int videoWithoutFile = await _world.AddVideoAsync(film.Coach, film.GameId);
            int clipWithoutFilm = await _world.AddClipAsync(film.Coach, videoWithoutFile, 10, 18);
            int cutupId = await _world.AddCutupAsync(film.Coach, "Mixed", film.ClipIds[0], clipWithoutFilm);

            using var context = _world.NewContext();
            var cutups = _world.Cutups(context);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => cutups.RequestRenderAsync(film.Coach, cutupId));
            Assert.Contains("Play 2", error.Message);
            Assert.Contains("Full game", error.Message);
            Assert.Contains("without a file", error.Message);
            Assert.Equal(CutupStatus.Draft, (await CutupAsync(cutupId)).Status);

            // The page can say so before the coach tries
            Assert.Equal(error.Message, (await cutups.GetCutupAsync(film.Coach, cutupId))!.RenderProblem);

            // Without that play it can be made
            int playId = (await CutupAsync(cutupId)).Clips!.Single(p => p.ClipId == clipWithoutFilm).Id;
            await cutups.RemovePlayAsync(film.Coach, playId);
            Assert.Null((await cutups.GetCutupAsync(film.Coach, cutupId))!.RenderProblem);
            Assert.True(await cutups.RequestRenderAsync(film.Coach, cutupId));
        }

        [Fact]
        public async Task NoVideoIsAskedFor_WhenTheServerCannotMakeOne()
        {
            var film = await AddFilmWithPlaysAsync();
            int cutupId = await _world.AddCutupAsync(film.Coach, "Third down", film.ClipIds);
            _world.Tools.Problem = "ffmpeg was not found.";

            using var context = _world.NewContext();

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _world.Cutups(context).RequestRenderAsync(film.Coach, cutupId));
            Assert.Equal("ffmpeg was not found.", error.Message);
            Assert.Equal(CutupStatus.Draft, (await CutupAsync(cutupId)).Status);
        }

        // ---------- Making the video ----------

        [Fact]
        public async Task TheVideo_IsMadeOfThePlaysInTheCutupsOrder_EachStartingWhereTheLastOneEnds()
        {
            var film = await AddFilmWithPlaysAsync();
            int cutupId = await _world.AddCutupAsync(film.Coach, "Third down", film.ClipIds);
            await AskForVideoAsync(film.Coach, cutupId);
            _world.Clock.Advance(TimeSpan.FromMinutes(2));

            Assert.True(await MakeNextAsync());

            // Every play was cut from its video's file in storage, from its start, for its length
            string input = "storage:" + film.VideoKey;
            Assert.Equal(new[] { (input, 100_000, 6_500), (input, 5_250, 4_000), (input, 21_100, 8_250) }, _world.Tools.Cuts);
            Assert.Equal(1, _world.Tools.Joins);

            var cutup = await CutupAsync(cutupId);
            Assert.Equal(CutupStatus.Ready, cutup.Status);
            Assert.Equal(100, cutup.ProgressPercent);
            Assert.Null(cutup.FailureReason);
            Assert.Equal(_world.Clock.GetUtcNow().UtcDateTime, cutup.RenderedAt);

            // The finished video is in the team's folder in storage, with the plays in the cutup's order
            Assert.StartsWith($"teams/{film.Coach.TeamId}/cutups/", cutup.StorageKey);
            Assert.EndsWith(".mp4", cutup.StorageKey);
            Assert.Equal(new byte[] { 1, 2, 3 }, _world.Storage.Files[cutup.StorageKey!]);

            // Business rule: each play starts where the one before it ends, measured on the video that was made.
            // 8.25 seconds is 247.5 pictures, so that play came out as 248 pictures: 8.267 seconds.
            var plays = cutup.Clips!.OrderBy(p => p.Position).ToList();
            Assert.Equal(new int?[] { 0, 6_500, 10_500 }, plays.Select(p => p.OutputOffsetMs));
            Assert.Equal(new int?[] { 6_500, 4_000, 8_267 }, plays.Select(p => p.OutputDurationMs));
            Assert.Equal(18_767, cutup.DurationMs);

            // The pages get the measured places and length
            using var context = _world.NewContext();
            var shown = await _world.Cutups(context).GetCutupAsync(film.Coach, cutupId);
            Assert.True(shown!.HasVideo);
            Assert.Equal(18_767, shown.DurationMs);
            Assert.Equal(new[] { 0, 6_500, 10_500 }, shown.Plays.Select(p => p.OffsetMs));
            Assert.Equal(8_267, shown.Plays[2].DurationMs);
            Assert.Null(shown.RenderProblem);
        }

        [Fact]
        public async Task TheCutupThatHasWaitedLongest_IsMadeFirst()
        {
            var film = await AddFilmWithPlaysAsync();
            int early = await _world.AddCutupAsync(film.Coach, "Asked first", film.ClipIds[0]);
            int late = await _world.AddCutupAsync(film.Coach, "Asked second", film.ClipIds[1]);

            await AskForVideoAsync(film.Coach, late);
            _world.Clock.Advance(TimeSpan.FromMinutes(-5));   // "early" was asked for five minutes before "late"
            await AskForVideoAsync(film.Coach, early);

            Assert.True(await MakeNextAsync());
            Assert.Equal(CutupStatus.Ready, (await CutupAsync(early)).Status);
            Assert.Equal(CutupStatus.Queued, (await CutupAsync(late)).Status);

            Assert.True(await MakeNextAsync());
            Assert.Equal(CutupStatus.Ready, (await CutupAsync(late)).Status);

            Assert.False(await MakeNextAsync());   // nothing is waiting any more
        }

        [Fact]
        public async Task WhenAPlayCannotBeCut_TheCutupFails_AndCanBeTriedAgain()
        {
            var film = await AddFilmWithPlaysAsync();
            int cutupId = await _world.AddCutupAsync(film.Coach, "Third down", film.ClipIds);
            await AskForVideoAsync(film.Coach, cutupId);
            _world.Tools.FailOnCut = 2;

            Assert.True(await MakeNextAsync());

            var cutup = await CutupAsync(cutupId);
            Assert.Equal(CutupStatus.Failed, cutup.Status);
            Assert.Equal("A play could not be cut out of its video.", cutup.FailureReason);
            Assert.Null(cutup.StorageKey);
            Assert.DoesNotContain(_world.Storage.Files.Keys, key => key.Contains("/cutups/"));
            Assert.Equal(0, _world.Tools.Joins);

            using (var context = _world.NewContext())
            {
                // A coach sees the reason; its plays are still fixed until it is turned back into a draft
                var shown = await _world.Cutups(context).GetCutupAsync(film.Coach, cutupId);
                Assert.Equal(cutup.FailureReason, shown!.FailureReason);
                Assert.False(shown.HasVideo);
            }

            // Trying again puts it back in the queue
            _world.Tools.FailOnCut = null;
            await AskForVideoAsync(film.Coach, cutupId);
            Assert.True(await MakeNextAsync());

            cutup = await CutupAsync(cutupId);
            Assert.Equal(CutupStatus.Ready, cutup.Status);
            Assert.Null(cutup.FailureReason);
        }

        [Fact]
        public async Task AVideoWhoseFileIsGone_FailsTheCutup_WithThePlayNamed()
        {
            var film = await AddFilmWithPlaysAsync();
            int cutupId = await _world.AddCutupAsync(film.Coach, "Third down", film.ClipIds);
            await AskForVideoAsync(film.Coach, cutupId);

            // The record lost its file after the cutup was put in the queue
            using (var context = _world.NewContext())
            {
                await context.Videos.Where(v => v.Id == film.VideoId).ExecuteUpdateAsync(u => u.SetProperty(v => v.StorageKey, (string?)null));
            }

            Assert.True(await MakeNextAsync());

            var cutup = await CutupAsync(cutupId);
            Assert.Equal(CutupStatus.Failed, cutup.Status);
            Assert.Contains("Play 1", cutup.FailureReason);
            Assert.Empty(_world.Tools.Cuts);
        }

        [Fact]
        public async Task ACutupTurnedBackIntoADraftWhileItWasBeingMade_KeepsNoVideo()
        {
            var film = await AddFilmWithPlaysAsync();
            int cutupId = await _world.AddCutupAsync(film.Coach, "Third down", film.ClipIds);
            await AskForVideoAsync(film.Coach, cutupId);

            // In the middle of the work the cutup stops being "Rendering" (a coach's change that slipped in)
            _world.Tools.WhileCutting = () => SetStatusAsync(cutupId, CutupStatus.Draft).GetAwaiter().GetResult();

            Assert.True(await MakeNextAsync());

            // The video that was made is thrown away: the plays may no longer be the ones it was made from
            var cutup = await CutupAsync(cutupId);
            Assert.Equal(CutupStatus.Draft, cutup.Status);
            Assert.Null(cutup.StorageKey);
            Assert.All(cutup.Clips!, play => Assert.Null(play.OutputOffsetMs));
            Assert.DoesNotContain(_world.Storage.Files.Keys, key => key.Contains("/cutups/"));
        }

        [Fact]
        public async Task ACutupThatWasBeingMadeWhenTheApiStopped_IsMadeAgain()
        {
            var film = await AddFilmWithPlaysAsync();
            int cutupId = await _world.AddCutupAsync(film.Coach, "Third down", film.ClipIds);
            await AskForVideoAsync(film.Coach, cutupId);
            await SetStatusAsync(cutupId, CutupStatus.Rendering);   // the API stopped in the middle of the work

            Assert.False(await MakeNextAsync());   // "Rendering" is not waiting, so nothing would ever pick it up

            using (var context = _world.NewContext())
            {
                Assert.Equal(1, await _world.Renderer(context).RequeueInterruptedAsync(CancellationToken.None));
            }

            Assert.True(await MakeNextAsync());
            Assert.Equal(CutupStatus.Ready, (await CutupAsync(cutupId)).Status);
        }

        // ---------- Around the making ----------

        [Fact]
        public async Task WhileItIsBeingMade_ACutupCanNeitherBeChangedNorDeletedNorTurnedBack()
        {
            var film = await AddFilmWithPlaysAsync();
            int cutupId = await _world.AddCutupAsync(film.Coach, "Third down", film.ClipIds);
            await AskForVideoAsync(film.Coach, cutupId);
            await SetStatusAsync(cutupId, CutupStatus.Rendering);

            using var context = _world.NewContext();
            var cutups = _world.Cutups(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => cutups.ReturnToDraftAsync(film.Coach, cutupId));
            await Assert.ThrowsAsync<InvalidOperationException>(() => cutups.DeleteAsync(film.Coach, cutupId));
            await Assert.ThrowsAsync<InvalidOperationException>(() => cutups.RequestRenderAsync(film.Coach, cutupId));

            // Its name and who it is shared with can still be changed
            Assert.True(await cutups.UpdateAsync(film.Coach, cutupId, new SaveCutupDto { Name = "Third down, week 3" }));
            Assert.True(await cutups.SetSharingAsync(film.Coach, cutupId, new SetCutupSharingDto { WholeTeam = true }));
        }

        [Fact]
        public async Task TurningACutupBackIntoADraft_ThrowsItsVideoAway_AndLetsItFollowItsClipsAgain()
        {
            var film = await AddFilmWithPlaysAsync();
            int cutupId = await _world.AddCutupAsync(film.Coach, "Third down", film.ClipIds);
            await AskForVideoAsync(film.Coach, cutupId);
            await MakeNextAsync();
            string cutupKey = (await CutupAsync(cutupId)).StorageKey!;

            using var context = _world.NewContext();
            var cutups = _world.Cutups(context);

            // The first play is trimmed after the video was made: the video, and the cutup's copy of the play, stay as they were
            await _world.Clips(context).UpdateAsync(film.Coach, film.ClipIds[0], new SaveClipDto { VideoId = film.VideoId, StartMs = 101_000, EndMs = 105_000, Title = "Sweep left, trimmed" });
            var copy = (await CutupAsync(cutupId)).Clips!.Single(p => p.ClipId == film.ClipIds[0]);
            Assert.Equal((100_000, 106_500, "Sweep left"), (copy.StartMs, copy.EndMs, copy.Title));

            Assert.True(await cutups.ReturnToDraftAsync(film.Coach, cutupId));

            var cutup = await CutupAsync(cutupId);
            Assert.Equal(CutupStatus.Draft, cutup.Status);
            Assert.Null(cutup.StorageKey);
            Assert.Null(cutup.DurationMs);
            Assert.Null(cutup.RenderedAt);
            Assert.False(_world.Storage.Files.ContainsKey(cutupKey));        // the cutup's video is gone
            Assert.True(_world.Storage.Files.ContainsKey(film.VideoKey));    // the game's video is not

            // As a draft it has caught up with the trimmed clip, and its plays can be changed again
            copy = cutup.Clips!.Single(p => p.ClipId == film.ClipIds[0]);
            Assert.Equal((101_000, 105_000, "Sweep left, trimmed"), (copy.StartMs, copy.EndMs, copy.Title));
            Assert.Null(copy.OutputOffsetMs);
            Assert.True(await cutups.RemovePlayAsync(film.Coach, copy.Id));

            var shown = await cutups.GetCutupAsync(film.Coach, cutupId);
            Assert.False(shown!.HasVideo);
            Assert.Equal(new[] { 0, 4_000 }, shown.Plays.Select(p => p.OffsetMs));   // added up again, from the plays that are left

            Assert.True(await cutups.ReturnToDraftAsync(film.Coach, cutupId));   // a draft stays a draft
        }

        [Fact]
        public async Task ACutupThatIsStillWaiting_CanBeTurnedBackIntoADraft()
        {
            var film = await AddFilmWithPlaysAsync();
            int cutupId = await _world.AddCutupAsync(film.Coach, "Third down", film.ClipIds);
            await AskForVideoAsync(film.Coach, cutupId);

            using (var context = _world.NewContext())
            {
                Assert.True(await _world.Cutups(context).ReturnToDraftAsync(film.Coach, cutupId));
            }

            Assert.Equal(CutupStatus.Draft, (await CutupAsync(cutupId)).Status);
            Assert.False(await MakeNextAsync());   // it is no longer in the queue
        }

        [Fact]
        public async Task DeletingACutup_DeletesItsVideoToo()
        {
            var film = await AddFilmWithPlaysAsync();
            int cutupId = await _world.AddCutupAsync(film.Coach, "Third down", film.ClipIds);
            await AskForVideoAsync(film.Coach, cutupId);
            await MakeNextAsync();
            string cutupKey = (await CutupAsync(cutupId)).StorageKey!;

            using var context = _world.NewContext();
            Assert.True(await _world.Cutups(context).DeleteAsync(film.Coach, cutupId));

            Assert.False(_world.Storage.Files.ContainsKey(cutupKey));
            Assert.True(_world.Storage.Files.ContainsKey(film.VideoKey));   // the game's video stays, with its plays
            Assert.Equal(3, await context.Clips.CountAsync(c => c.VideoId == film.VideoId));
        }

        // ---------- Who may watch ----------

        [Fact]
        public async Task TheFinishedVideo_IsPlayedOnlyByThoseTheCutupIsSharedWith()
        {
            var film = await AddFilmWithPlaysAsync();
            var receiver = await _world.AddPlayerAsync(film.Coach, "Walker", "Receivers");
            var lineman = await _world.AddPlayerAsync(film.Coach, "Petrov", "Offensive Line");
            var outsider = await _world.AddTeamAsync("Other Team");

            int cutupId = await _world.AddCutupAsync(film.Coach, "Routes", film.ClipIds);
            int receivers = await _world.GroupIdAsync(film.Coach, "Receivers");

            using (var before = _world.NewContext())
            {
                var draft = _world.Cutups(before);
                await draft.SetSharingAsync(film.Coach, cutupId, new SetCutupSharingDto { GroupIds = new List<int> { receivers } });

                // Before the video has been made there is nothing to play, for anyone
                Assert.Null(await draft.GetFileAsync(film.Coach, cutupId));
                Assert.Null(await draft.GetFileAsync(receiver, cutupId));

                await draft.RequestRenderAsync(film.Coach, cutupId);
            }

            await MakeNextAsync();
            string key = (await CutupAsync(cutupId)).StorageKey!;

            using var context = _world.NewContext();
            var cutups = _world.Cutups(context);

            // Business rule: a player reaches only film shared with them, with one of their groups, or with the team
            Assert.Equal("/test-storage/" + key, (await cutups.GetFileAsync(film.Coach, cutupId))!.Path);
            Assert.Equal("/test-storage/" + key, (await cutups.GetFileAsync(receiver, cutupId))!.Path);
            await Assert.ThrowsAsync<ForbiddenException>(() => cutups.GetFileAsync(lineman, cutupId));
            Assert.Null(await cutups.GetFileAsync(outsider, cutupId));

            // With signed links the player is sent on to storage instead
            _world.Storage.SignedLinks = true;
            var file = await cutups.GetFileAsync(receiver, cutupId);
            Assert.Equal($"https://storage.test/{key}?play", file!.SignedLink);
            await Assert.ThrowsAsync<ForbiddenException>(() => cutups.GetFileAsync(lineman, cutupId));
        }

        [Fact]
        public async Task OnlyCoaches_AskForAVideoOrTurnACutupBack()
        {
            var film = await AddFilmWithPlaysAsync();
            var player = await _world.AddPlayerAsync(film.Coach, "Walker", "Receivers");
            var outsider = await _world.AddTeamAsync("Other Team");
            int cutupId = await _world.AddCutupAsync(film.Coach, "Routes", film.ClipIds);

            using var context = _world.NewContext();
            var cutups = _world.Cutups(context);
            await cutups.SetSharingAsync(film.Coach, cutupId, new SetCutupSharingDto { WholeTeam = true });

            await Assert.ThrowsAsync<ForbiddenException>(() => cutups.RequestRenderAsync(player, cutupId));
            await Assert.ThrowsAsync<ForbiddenException>(() => cutups.ReturnToDraftAsync(player, cutupId));

            // Another team's cutup does not exist for a coach of this one
            Assert.False(await cutups.RequestRenderAsync(outsider, cutupId));
            Assert.False(await cutups.ReturnToDraftAsync(outsider, cutupId));
            Assert.Equal(CutupStatus.Draft, (await CutupAsync(cutupId)).Status);

            // A player is told nothing about why a video could not be made
            await cutups.RequestRenderAsync(film.Coach, cutupId);
            _world.Tools.FailOnCut = 1;
            await MakeNextAsync();

            using var afterwards = _world.NewContext();
            var shown = await _world.Cutups(afterwards).GetCutupAsync(player, cutupId);
            Assert.Equal(CutupStatus.Failed, shown!.Status);
            Assert.Null(shown.FailureReason);
            Assert.Null(shown.RenderProblem);
        }
    }
}
