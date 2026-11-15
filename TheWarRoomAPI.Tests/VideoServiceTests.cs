using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Uploading the video of a game: start, send the file, complete. And playing and deleting it.
    public class VideoServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        private static StartUploadDto Upload(int gameId, string fileName = "week3.mp4", long sizeBytes = 5000)
        {
            return new StartUploadDto { GameId = gameId, Title = "  End zone angle ", FileName = fileName, SizeBytes = sizeBytes };
        }

        private static MemoryStream FileOf(int sizeBytes)
        {
            return new MemoryStream(new byte[sizeBytes]);
        }

        // A video that went through the whole upload and is ready
        private async Task<int> UploadedVideoAsync(CurrentUser coach, int gameId)
        {
            using var context = _world.NewContext();
            var service = _world.Videos(context);

            var ticket = (await service.StartUploadAsync(coach, Upload(gameId)))!;
            await service.ReceiveFileAsync(coach, ticket.VideoId, FileOf(5000), CancellationToken.None);
            await service.CompleteUploadAsync(coach, ticket.VideoId, CancellationToken.None);

            return ticket.VideoId;
        }

        private async Task<Video> VideoAsync(int id)
        {
            using var context = _world.NewContext();
            return await context.Videos.SingleAsync(v => v.Id == id);
        }

        // ---------- Starting ----------

        [Fact]
        public async Task StartingAnUpload_MakesARecordThatWaitsForItsFile()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();
            var ticket = await _world.Videos(context).StartUploadAsync(headCoach, Upload(film.GameId));

            Assert.NotNull(ticket);
            Assert.Null(ticket.UploadUrl);   // this storage has no signed links: the file goes through the site
            Assert.Equal("video/mp4", ticket.ContentType);

            var video = await VideoAsync(ticket.VideoId);
            Assert.Equal(VideoStatus.Uploading, video.Status);
            Assert.Equal("End zone angle", video.Title);
            Assert.Equal("week3.mp4", video.OriginalFileName);
            Assert.Equal(headCoach.TeamId, video.TeamId);
            Assert.Equal(headCoach.Id, video.UploadedById);
            Assert.Null(video.DurationMs);

            // The file's place in storage is made up by the server: the team's folder and a name nobody can guess
            Assert.StartsWith($"teams/{headCoach.TeamId}/videos/", video.StorageKey);
            Assert.EndsWith(".mp4", video.StorageKey);
            Assert.DoesNotContain("week3", video.StorageKey);
        }

        [Fact]
        public async Task WithSignedLinks_TheBrowserIsToldWhereToSendTheFile()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            _world.Storage.SignedLinks = true;

            using var context = _world.NewContext();
            var ticket = (await _world.Videos(context).StartUploadAsync(headCoach, Upload(film.GameId, "week3.MOV")))!;

            var video = await VideoAsync(ticket.VideoId);
            Assert.Equal($"https://storage.test/{video.StorageKey}?upload", ticket.UploadUrl);
            Assert.Equal("video/quicktime", ticket.ContentType);
            Assert.EndsWith(".mov", video.StorageKey);
        }

        [Theory]
        [InlineData("highlights.avi")]
        [InlineData("notes.txt")]
        [InlineData("week3.mp4.exe")]
        [InlineData("week3")]
        public async Task OnlyMp4AndMovFiles_CanBeUploaded(string fileName)
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Videos(context).StartUploadAsync(headCoach, Upload(film.GameId, fileName)));
            Assert.Equal(1, await context.Videos.CountAsync());   // only the video the test began with
        }

        [Fact]
        public async Task TheFileName_IsKeptWithoutItsFolders()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();
            var ticket = (await _world.Videos(context).StartUploadAsync(headCoach, Upload(film.GameId, @"C:\Film\2026\week3.M4V")))!;

            Assert.Equal("week3.M4V", (await VideoAsync(ticket.VideoId)).OriginalFileName);
        }

        [Fact]
        public async Task AFileOverTheLargestSize_IsRefusedBeforeAnythingIsSent()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Videos(context).StartUploadAsync(headCoach, Upload(film.GameId, sizeBytes: _world.Film.MaxUploadBytes + 1)));
            Assert.Contains("larger than", error.Message);
        }

        [Fact]
        public async Task NothingIsUploaded_WhenTheServerCouldNotCheckOrKeepIt()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();
            var service = _world.Videos(context);

            _world.Tools.Problem = "ffmpeg was not found.";
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartUploadAsync(headCoach, Upload(film.GameId)));
            Assert.Equal("ffmpeg was not found.", error.Message);
            Assert.Equal("ffmpeg was not found.", service.GetSetup(headCoach).Problem);

            _world.Tools.Problem = null;
            _world.Storage.Problem = "No bucket has been named.";
            error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartUploadAsync(headCoach, Upload(film.GameId)));
            Assert.Equal("No bucket has been named.", error.Message);

            _world.Storage.Problem = null;
            Assert.Null(service.GetSetup(headCoach).Problem);
            Assert.NotNull(await service.StartUploadAsync(headCoach, Upload(film.GameId)));
        }

        // ---------- Sending the file ----------

        [Fact]
        public async Task TheFile_IsStoredUnderTheRecordsKey()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();
            var service = _world.Videos(context);
            var ticket = (await service.StartUploadAsync(headCoach, Upload(film.GameId)))!;

            Assert.True(await service.ReceiveFileAsync(headCoach, ticket.VideoId, FileOf(4321), CancellationToken.None));

            string key = (await VideoAsync(ticket.VideoId)).StorageKey!;
            Assert.Equal(4321, _world.Storage.Files[key].Length);
        }

        [Fact]
        public async Task AFileLargerThanTheLargestSize_IsNotStored()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();
            var service = _world.Videos(context);

            // The browser said 5000 bytes and then sends more than is taken
            var ticket = (await service.StartUploadAsync(headCoach, Upload(film.GameId)))!;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ReceiveFileAsync(headCoach, ticket.VideoId, FileOf((int)_world.Film.MaxUploadBytes + 1), CancellationToken.None));
            Assert.Empty(_world.Storage.Files);
        }

        [Fact]
        public async Task WithSignedLinks_TheApiDoesNotTakeTheFileItself()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            _world.Storage.SignedLinks = true;

            using var context = _world.NewContext();
            var service = _world.Videos(context);
            var ticket = (await service.StartUploadAsync(headCoach, Upload(film.GameId)))!;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ReceiveFileAsync(headCoach, ticket.VideoId, FileOf(100), CancellationToken.None));
        }

        [Fact]
        public async Task AVideoThatIsReady_TakesNoOtherFile()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int videoId = await UploadedVideoAsync(headCoach, film.GameId);

            using var context = _world.NewContext();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Videos(context).ReceiveFileAsync(headCoach, videoId, FileOf(100), CancellationToken.None));
        }

        // ---------- Completing ----------

        [Fact]
        public async Task Completing_ChecksTheFileItself_AndMakesTheVideoReady()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            _world.Tools.Facts.DurationMs = 83_500;

            using var context = _world.NewContext();
            var service = _world.Videos(context);
            var ticket = (await service.StartUploadAsync(headCoach, Upload(film.GameId, sizeBytes: 9999)))!;
            await service.ReceiveFileAsync(headCoach, ticket.VideoId, FileOf(4321), CancellationToken.None);

            var result = await service.CompleteUploadAsync(headCoach, ticket.VideoId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(VideoStatus.Ready, result.Status);
            Assert.True(result.HasFile);
            Assert.Equal(83_500, result.DurationMs);
            Assert.Equal(4321, result.SizeBytes);   // the size of what arrived, not what the browser announced

            var video = await VideoAsync(ticket.VideoId);
            Assert.Equal(new[] { "storage:" + video.StorageKey }, _world.Tools.Probed);

            // Now it can be cut into plays, and a play has to lie inside the measured length
            var clips = _world.Clips(context);
            Assert.NotNull(await clips.CreateAsync(headCoach, new SaveClipDto { VideoId = video.Id, StartMs = 80_000, EndMs = 83_500 }));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => clips.CreateAsync(headCoach, new SaveClipDto { VideoId = video.Id, StartMs = 80_000, EndMs = 83_501 }));
        }

        [Fact]
        public async Task Completing_BeforeTheFileHasArrived_IsRefused_AndCanBeTriedAgain()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();
            var service = _world.Videos(context);
            var ticket = (await service.StartUploadAsync(headCoach, Upload(film.GameId)))!;

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteUploadAsync(headCoach, ticket.VideoId, CancellationToken.None));
            Assert.Contains("has not arrived", error.Message);
            Assert.Equal(VideoStatus.Uploading, (await VideoAsync(ticket.VideoId)).Status);

            await service.ReceiveFileAsync(headCoach, ticket.VideoId, FileOf(5000), CancellationToken.None);
            var result = await service.CompleteUploadAsync(headCoach, ticket.VideoId, CancellationToken.None);
            Assert.Equal(VideoStatus.Ready, result!.Status);
        }

        [Fact]
        public async Task AFileThatIsNotAVideo_Fails_AndIsThrownAway()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            _world.Tools.Unreadable = true;

            using var context = _world.NewContext();
            var service = _world.Videos(context);
            var ticket = (await service.StartUploadAsync(headCoach, Upload(film.GameId)))!;
            await service.ReceiveFileAsync(headCoach, ticket.VideoId, FileOf(5000), CancellationToken.None);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteUploadAsync(headCoach, ticket.VideoId, CancellationToken.None));

            var video = await VideoAsync(ticket.VideoId);
            Assert.Equal(VideoStatus.Failed, video.Status);
            Assert.Equal(error.Message, video.FailureReason);
            Assert.Null(video.StorageKey);
            Assert.Empty(_world.Storage.Files);

            // The record tells the coach what happened, and nothing can be cut from it
            var shown = await service.GetVideoAsync(headCoach, ticket.VideoId);
            Assert.False(shown!.HasFile);
            Assert.Equal(error.Message, shown.FailureReason);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Clips(context).CreateAsync(headCoach, new SaveClipDto { VideoId = video.Id, StartMs = 0, EndMs = 1000 }));
        }

        [Fact]
        public async Task AVideoThatDoesNotPlayInEveryBrowser_Fails_WithTheReason()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            _world.Tools.Facts.VideoCodec = "hevc";

            using var context = _world.NewContext();
            var service = _world.Videos(context);
            var ticket = (await service.StartUploadAsync(headCoach, Upload(film.GameId, "phone.mov")))!;
            await service.ReceiveFileAsync(headCoach, ticket.VideoId, FileOf(5000), CancellationToken.None);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteUploadAsync(headCoach, ticket.VideoId, CancellationToken.None));

            Assert.Contains("HEVC", error.Message);
            Assert.Contains("H.264", error.Message);
            Assert.Equal(VideoStatus.Failed, (await VideoAsync(ticket.VideoId)).Status);
        }

        [Fact]
        public async Task AFileSentStraightToStorage_IsMeasuredThere_NotByWhatTheBrowserSaid()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            _world.Storage.SignedLinks = true;

            using var context = _world.NewContext();
            var service = _world.Videos(context);

            // The browser announced 5000 bytes and then sent far more to the signed link
            var ticket = (await service.StartUploadAsync(headCoach, Upload(film.GameId)))!;
            string key = (await VideoAsync(ticket.VideoId)).StorageKey!;
            _world.Storage.BrowserSends(key, (int)_world.Film.MaxUploadBytes + 1);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteUploadAsync(headCoach, ticket.VideoId, CancellationToken.None));

            Assert.Contains("larger than", error.Message);
            Assert.Equal(VideoStatus.Failed, (await VideoAsync(ticket.VideoId)).Status);
            Assert.Empty(_world.Storage.Files);
            Assert.Empty(_world.Tools.Probed);   // it was not even looked at
        }

        [Fact]
        public async Task CompletingTwice_GivesTheSameAnswer()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int videoId = await UploadedVideoAsync(headCoach, film.GameId);

            using var context = _world.NewContext();
            var again = await _world.Videos(context).CompleteUploadAsync(headCoach, videoId, CancellationToken.None);

            Assert.Equal(VideoStatus.Ready, again!.Status);
            Assert.Single(_world.Tools.Probed);   // the file was checked once
        }

        // ---------- Playing ----------

        [Fact]
        public async Task AReadyVideo_IsPlayedFromStorage()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int videoId = await UploadedVideoAsync(headCoach, film.GameId);
            string key = (await VideoAsync(videoId)).StorageKey!;

            using var context = _world.NewContext();
            var service = _world.Videos(context);

            // Without signed links the API sends the file itself
            var file = await service.GetFileAsync(headCoach, videoId);
            Assert.Equal("/test-storage/" + key, file!.Path);
            Assert.Null(file.SignedLink);
            Assert.Equal("video/mp4", file.ContentType);

            // With signed links the caller is sent on to storage
            _world.Storage.SignedLinks = true;
            file = await service.GetFileAsync(headCoach, videoId);
            Assert.Equal($"https://storage.test/{key}?play", file!.SignedLink);
            Assert.Null(file.Path);
        }

        [Fact]
        public async Task AVideoWithoutAFile_HasNothingToPlay()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);   // its video is a record without a file

            using var context = _world.NewContext();
            var service = _world.Videos(context);

            Assert.Null(await service.GetFileAsync(headCoach, film.VideoId));
            Assert.False((await service.GetVideoAsync(headCoach, film.VideoId))!.HasFile);

            // An upload that has not been completed cannot be played either
            var ticket = (await service.StartUploadAsync(headCoach, Upload(film.GameId)))!;
            await service.ReceiveFileAsync(headCoach, ticket.VideoId, FileOf(5000), CancellationToken.None);
            Assert.Null(await service.GetFileAsync(headCoach, ticket.VideoId));
            Assert.False((await service.GetVideoAsync(headCoach, ticket.VideoId))!.HasFile);
        }

        // ---------- Deleting ----------

        [Fact]
        public async Task DeletingAVideo_RemovesItsPlaysAndItsFile()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int videoId = await UploadedVideoAsync(headCoach, film.GameId);
            await _world.AddClipAsync(headCoach, videoId, 5, 12, film.OpponentId);
            await _world.AddClipAsync(headCoach, videoId, 20, 26);
            Assert.Single(_world.Storage.Files);

            using var context = _world.NewContext();
            Assert.True(await _world.Videos(context).DeleteAsync(headCoach, videoId, CancellationToken.None));

            using var check = _world.NewContext();
            Assert.False(await check.Videos.AnyAsync(v => v.Id == videoId));
            Assert.False(await check.Clips.AnyAsync(c => c.VideoId == videoId));
            Assert.Equal(0, await check.PlayTags.CountAsync());
            Assert.Empty(_world.Storage.Files);

            // The game is still there, and can be deleted once it has no video left
            Assert.True(await check.Games.AnyAsync(g => g.Id == film.GameId));
        }

        [Fact]
        public async Task AnUploadThatNeverFinished_CanBeDeleted()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            using var context = _world.NewContext();
            var service = _world.Videos(context);
            var ticket = (await service.StartUploadAsync(headCoach, Upload(film.GameId)))!;
            await service.ReceiveFileAsync(headCoach, ticket.VideoId, FileOf(5000), CancellationToken.None);

            Assert.True(await service.DeleteAsync(headCoach, ticket.VideoId, CancellationToken.None));
            Assert.Empty(_world.Storage.Files);
        }

        [Fact]
        public async Task AVideoWhosePlaysAreInACutup_CannotBeDeleted()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int videoId = await UploadedVideoAsync(headCoach, film.GameId);
            int clipId = await _world.AddClipAsync(headCoach, videoId, 5, 12);

            using var context = _world.NewContext();
            var cutups = _world.Cutups(context);
            var cutup = await cutups.CreateAsync(headCoach, new SaveCutupDto { Name = "Red zone" });
            await cutups.AddClipsAsync(headCoach, cutup.Id, new AddCutupClipsDto { ClipIds = new List<int> { clipId } });

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Videos(context).DeleteAsync(headCoach, videoId, CancellationToken.None));

            Assert.Contains("Red zone", error.Message);
            Assert.Single(_world.Storage.Files);
            Assert.True(await context.Videos.AnyAsync(v => v.Id == videoId));
        }

        // ---------- Who may do this ----------

        [Fact]
        public async Task Players_CannotUploadPlayOrDeleteGameVideo()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var film = await _world.AddFilmAsync(headCoach);
            int videoId = await UploadedVideoAsync(headCoach, film.GameId);

            using var context = _world.NewContext();
            var service = _world.Videos(context);

            Assert.Throws<ForbiddenException>(() => service.GetSetup(player));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.StartUploadAsync(player, Upload(film.GameId)));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.ReceiveFileAsync(player, videoId, FileOf(10), CancellationToken.None));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.CompleteUploadAsync(player, videoId, CancellationToken.None));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetFileAsync(player, videoId));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteAsync(player, videoId, CancellationToken.None));
        }

        [Fact]
        public async Task AnotherTeamsVideo_CannotBeReached()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int videoId = await UploadedVideoAsync(headCoach, film.GameId);
            var outsider = await _world.AddTeamAsync("Other Team");

            using var context = _world.NewContext();
            var service = _world.Videos(context);

            Assert.Null(await service.StartUploadAsync(outsider, Upload(film.GameId)));   // somebody else's game
            Assert.False(await service.ReceiveFileAsync(outsider, videoId, FileOf(10), CancellationToken.None));
            Assert.Null(await service.CompleteUploadAsync(outsider, videoId, CancellationToken.None));
            Assert.Null(await service.GetFileAsync(outsider, videoId));
            Assert.False(await service.DeleteAsync(outsider, videoId, CancellationToken.None));

            // Nothing was changed
            Assert.Single(_world.Storage.Files);
            Assert.Equal(VideoStatus.Ready, (await VideoAsync(videoId)).Status);
        }
    }
}
