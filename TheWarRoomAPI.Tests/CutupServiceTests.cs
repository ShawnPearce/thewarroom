using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Cutups: plays in a chosen order, what can change while one is a draft,
    // and which players a cutup reaches once it is shared
    public class CutupServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        // A team with film, and three clips of 8, 6 and 12 seconds
        private async Task<(CurrentUser HeadCoach, TestWorld.TestFilm Film, int[] ClipIds)> TeamWithClipsAsync()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach, "Riverside Raiders");

            int first = await _world.AddClipAsync(headCoach, film.VideoId, 60, 68, film.OpponentId, PlayType.Run, 1, 10);
            int second = await _world.AddClipAsync(headCoach, film.VideoId, 100, 106, film.OpponentId, PlayType.Pass, 2, 7);
            int third = await _world.AddClipAsync(headCoach, film.VideoId, 150, 162);

            return (headCoach, film, new[] { first, second, third });
        }

        private async Task<int> AddCutupAsync(CurrentUser coach, string name, params int[] clipIds)
        {
            using var context = _world.NewContext();
            var service = _world.Cutups(context);

            var cutup = await service.CreateAsync(coach, new SaveCutupDto { Name = name });
            if (clipIds.Length > 0)
            {
                await service.AddClipsAsync(coach, cutup.Id, new AddCutupClipsDto { ClipIds = clipIds.ToList() });
            }

            return cutup.Id;
        }

        private async Task SetStatusAsync(int cutupId, CutupStatus status)
        {
            using var context = _world.NewContext();
            var cutup = await context.Cutups.SingleAsync(c => c.Id == cutupId);
            cutup.Status = status;
            await context.SaveChangesAsync();
        }

        // ---------- Building a cutup ----------

        [Fact]
        public async Task ANewCutup_IsADraftWithNoPlays_ForTheMondayOfItsWeek()
        {
            var headCoach = await _world.AddTeamAsync();

            using var context = _world.NewContext();
            var cutup = await _world.Cutups(context).CreateAsync(headCoach, new SaveCutupDto
            {
                Name = "  Raiders run game ",
                Description = " ",
                ForWeekOf = TestWorld.Today   // a Wednesday
            });

            Assert.Equal("Raiders run game", cutup.Name);
            Assert.Null(cutup.Description);
            Assert.Equal(TestWorld.ThisMonday, cutup.ForWeekOf);
            Assert.Equal(CutupStatus.Draft, cutup.Status);
            Assert.False(cutup.HasVideo);
            Assert.Equal(0, cutup.PlayCount);
            Assert.True(cutup.CanManage);
        }

        [Fact]
        public async Task AddedClips_GoToTheEndInTheOrderChosen_AndEachPlayStartsWhereThePreviousOneEnds()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();

            // The third clip first, then the first: the order of the cutup is the coach's, not the game's
            int cutupId = await AddCutupAsync(headCoach, "Raiders", clips[2], clips[0]);

            using var context = _world.NewContext();
            var service = _world.Cutups(context);
            await service.AddClipsAsync(headCoach, cutupId, new AddCutupClipsDto { ClipIds = { clips[1] } });

            var cutup = await service.GetCutupAsync(headCoach, cutupId);

            Assert.Equal(new int?[] { clips[2], clips[0], clips[1] }, cutup!.Plays.Select(p => p.ClipId));
            Assert.Equal(new[] { 1, 2, 3 }, cutup.Plays.Select(p => p.Position));
            Assert.Equal(new[] { 12_000, 8_000, 6_000 }, cutup.Plays.Select(p => p.DurationMs));
            Assert.Equal(new[] { 0, 12_000, 20_000 }, cutup.Plays.Select(p => p.OffsetMs));   // no time between plays
            Assert.Equal(26_000, cutup.DurationMs);
            Assert.Equal(3, cutup.PlayCount);

            // A play keeps where it is in its source video, and the coach sees how it is tagged
            Assert.Equal(150_000, cutup.Plays[0].StartMs);
            Assert.Equal("Riverside Raiders at Test Tigers", cutup.Plays[0].Matchup);
            Assert.Null(cutup.Plays[0].Tag);
            Assert.Equal(PlayType.Run, cutup.Plays[1].Tag!.PlayType);
        }

        [Fact]
        public async Task AddingAPlayThatIsAlreadyThere_LeavesItWhereItIs()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            int cutupId = await AddCutupAsync(headCoach, "Raiders", clips[0], clips[1]);

            using var context = _world.NewContext();
            var service = _world.Cutups(context);
            await service.AddClipsAsync(headCoach, cutupId, new AddCutupClipsDto { ClipIds = { clips[1], clips[2], clips[2] } });

            var cutup = await service.GetCutupAsync(headCoach, cutupId);
            Assert.Equal(new int?[] { clips[0], clips[1], clips[2] }, cutup!.Plays.Select(p => p.ClipId));
        }

        [Fact]
        public async Task MovingAndRemovingPlays_KeepsTheOrderNumberedFromOne()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            int cutupId = await AddCutupAsync(headCoach, "Raiders", clips);

            using var context = _world.NewContext();
            var service = _world.Cutups(context);

            async Task<List<CutupPlayDto>> PlaysAsync() => (await service.GetCutupAsync(headCoach, cutupId))!.Plays;

            var plays = await PlaysAsync();
            int firstPlay = plays[0].Id, lastPlay = plays[2].Id;

            // The last play becomes the first, one step at a time
            await service.MovePlayAsync(headCoach, lastPlay, MoveDirection.Up);
            await service.MovePlayAsync(headCoach, lastPlay, MoveDirection.Up);
            await service.MovePlayAsync(headCoach, lastPlay, MoveDirection.Up);   // already first: nothing changes
            Assert.Equal(new int?[] { clips[2], clips[0], clips[1] }, (await PlaysAsync()).Select(p => p.ClipId));

            await service.MovePlayAsync(headCoach, firstPlay, MoveDirection.Down);
            Assert.Equal(new int?[] { clips[2], clips[1], clips[0] }, (await PlaysAsync()).Select(p => p.ClipId));

            // Taking out the middle play closes the gap
            await service.RemovePlayAsync(headCoach, (await PlaysAsync())[1].Id);
            plays = await PlaysAsync();
            Assert.Equal(new int?[] { clips[2], clips[0] }, plays.Select(p => p.ClipId));
            Assert.Equal(new[] { 1, 2 }, plays.Select(p => p.Position));
            Assert.Equal(new[] { 0, 12_000 }, plays.Select(p => p.OffsetMs));
        }

        [Fact]
        public async Task PlaysFromAnotherTeamsFilm_CannotBeAdded()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            var outsider = await _world.AddTeamAsync("Other Team");
            int theirCutup = await AddCutupAsync(outsider, "Scouting");

            using var context = _world.NewContext();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _world.Cutups(context).AddClipsAsync(
                outsider, theirCutup, new AddCutupClipsDto { ClipIds = { clips[0] } }));

            Assert.Contains("not in this team's film", error.Message);
        }

        // ---------- Drafts follow their clips; finished film does not change ----------

        [Fact]
        public async Task ADraft_FollowsLaterChangesToItsClips_ButAFinishedCutupKeepsItsOwnCopy()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            int draft = await AddCutupAsync(headCoach, "Still being built", clips[0]);
            int finished = await AddCutupAsync(headCoach, "Already shared", clips[0]);
            await SetStatusAsync(finished, CutupStatus.Ready);

            // The coach trims the clip afterwards: it was 60 to 68 seconds, now 62 to 67
            using var context = _world.NewContext();
            await _world.Clips(context).UpdateAsync(headCoach, clips[0], new SaveClipDto
            {
                VideoId = film.VideoId,
                StartMs = 62_000,
                EndMs = 67_000,
                Title = "Trimmed"
            });

            var cutups = _world.Cutups(context);
            var draftPlay = (await cutups.GetCutupAsync(headCoach, draft))!.Plays.Single();
            var finishedPlay = (await cutups.GetCutupAsync(headCoach, finished))!.Plays.Single();

            Assert.Equal(62_000, draftPlay.StartMs);
            Assert.Equal(5_000, draftPlay.DurationMs);
            Assert.Equal("Trimmed", draftPlay.Title);

            Assert.Equal(60_000, finishedPlay.StartMs);   // film that was made stays as it was
            Assert.Equal(8_000, finishedPlay.DurationMs);
            Assert.Null(finishedPlay.Title);
        }

        [Theory]
        [InlineData(CutupStatus.Queued)]
        [InlineData(CutupStatus.Rendering)]
        [InlineData(CutupStatus.Ready)]
        public async Task OnceACutupIsNoLongerADraft_ItsPlaysCannotBeChanged(CutupStatus status)
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            int cutupId = await AddCutupAsync(headCoach, "Raiders", clips[0], clips[1]);
            await SetStatusAsync(cutupId, status);

            using var context = _world.NewContext();
            var service = _world.Cutups(context);
            int playId = (await service.GetCutupAsync(headCoach, cutupId))!.Plays[0].Id;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddClipsAsync(headCoach, cutupId, new AddCutupClipsDto { ClipIds = { clips[2] } }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.MovePlayAsync(headCoach, playId, MoveDirection.Down));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RemovePlayAsync(headCoach, playId));

            // Its name and who it is shared with can still change
            Assert.True(await service.UpdateAsync(headCoach, cutupId, new SaveCutupDto { Name = "Raiders (final)" }));
            Assert.True(await service.SetSharingAsync(headCoach, cutupId, new SetCutupSharingDto { WholeTeam = true }));
        }

        // ---------- Sharing ----------

        [Fact]
        public async Task ACutup_IsSharedWithTheTeam_ItsGroups_AndItsActivePlayers()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            var coach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int receivers = await _world.GroupIdAsync(headCoach, "Receivers");
            int cutupId = await AddCutupAsync(headCoach, "Raiders", clips);

            var outsider = await _world.AddTeamAsync("Other Team");
            var theirPlayer = await _world.AddPlayerAsync(outsider, "Stranger", "Receivers");
            int theirGroup = await _world.GroupIdAsync(outsider, "Receivers");

            using var context = _world.NewContext();
            var service = _world.Cutups(context);

            // A group or a player of another team, and a coach in the list of players, are all refused
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SetSharingAsync(headCoach, cutupId, new SetCutupSharingDto { GroupIds = { theirGroup } }));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SetSharingAsync(headCoach, cutupId, new SetCutupSharingDto { PlayerIds = { theirPlayer.Id } }));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SetSharingAsync(headCoach, cutupId, new SetCutupSharingDto { PlayerIds = { coach.Id } }));

            // Any coach can share
            await service.SetSharingAsync(coach, cutupId, new SetCutupSharingDto { GroupIds = { receivers }, PlayerIds = { player.Id } });
            var sharing = (await service.GetCutupAsync(headCoach, cutupId))!.Sharing!;
            Assert.False(sharing.WholeTeam);
            Assert.Equal("Receivers", Assert.Single(sharing.Groups).Name);
            Assert.Equal("Walker", Assert.Single(sharing.Players).LastName);

            // Setting it again replaces what was there
            await service.SetSharingAsync(headCoach, cutupId, new SetCutupSharingDto { WholeTeam = true });
            var cutup = await service.GetCutupAsync(headCoach, cutupId);
            Assert.True(cutup!.Sharing!.WholeTeam);
            Assert.Empty(cutup.Sharing.Groups);
            Assert.Empty(cutup.Sharing.Players);
            Assert.Equal(new[] { "Whole team" }, cutup.SharedWith);
        }

        [Fact]
        public async Task APlayerWhoHasBeenDeactivated_CannotBeGivenFilm()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int cutupId = await AddCutupAsync(headCoach, "Raiders", clips);

            using (var setup = _world.NewContext())
            {
                (await setup.Users.SingleAsync(u => u.Id == player.Id)).IsActive = false;
                await setup.SaveChangesAsync();
            }

            using var context = _world.NewContext();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _world.Cutups(context).SetSharingAsync(
                headCoach, cutupId, new SetCutupSharingDto { PlayerIds = { player.Id } }));

            Assert.Contains("active player", error.Message);
        }

        [Fact]
        public async Task APlayer_ReachesOnlyFilmSharedWithThem_TheirGroup_OrTheWholeTeam()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            var receiver = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var lineman = await _world.AddPlayerAsync(headCoach, "Kowalski", "Offensive Line");
            int receivers = await _world.GroupIdAsync(headCoach, "Receivers");

            int forTheTeam = await AddCutupAsync(headCoach, "For the team", clips[0]);
            int forReceivers = await AddCutupAsync(headCoach, "For the receivers", clips[1]);
            int forTheLineman = await AddCutupAsync(headCoach, "For one lineman", clips[2]);
            int notShared = await AddCutupAsync(headCoach, "Not shared", clips[0]);

            using var context = _world.NewContext();
            var service = _world.Cutups(context);

            await service.SetSharingAsync(headCoach, forTheTeam, new SetCutupSharingDto { WholeTeam = true });
            await service.SetSharingAsync(headCoach, forReceivers, new SetCutupSharingDto { GroupIds = { receivers } });
            await service.SetSharingAsync(headCoach, forTheLineman, new SetCutupSharingDto { PlayerIds = { lineman.Id } });

            async Task<int[]> SeenByAsync(CurrentUser user) => (await service.GetCutupsAsync(user)).Select(c => c.Id).Order().ToArray();

            Assert.Equal(new[] { forTheTeam, forReceivers }, await SeenByAsync(receiver));
            Assert.Equal(new[] { forTheTeam, forTheLineman }, await SeenByAsync(lineman));
            Assert.Equal(4, (await SeenByAsync(headCoach)).Length);

            // Opening one directly follows the same rule
            Assert.NotNull(await service.GetCutupAsync(receiver, forReceivers));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetCutupAsync(receiver, forTheLineman));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetCutupAsync(receiver, notShared));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetCutupAsync(lineman, forReceivers));

            // A player who joins the group afterwards gets the group's film as well
            using (var setup = _world.NewContext())
            {
                setup.PlayerGroupAssignments.Add(new PlayerGroupAssignment { PlayerId = lineman.Id, PositionGroupId = receivers });
                await setup.SaveChangesAsync();
            }
            var linemanNow = await _world.AsAsync(lineman.Id);
            Assert.Equal(new[] { forTheTeam, forReceivers, forTheLineman }, await SeenByAsync(linemanNow));

            // And film that is no longer shared disappears for the player
            await service.SetSharingAsync(headCoach, forTheTeam, new SetCutupSharingDto());
            Assert.Equal(new[] { forReceivers }, await SeenByAsync(receiver));
        }

        [Fact]
        public async Task APlayer_GetsTheFilm_NotTheCoachesAnalysisOrTheSharingList()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int cutupId = await AddCutupAsync(headCoach, "For the team", clips[0], clips[1]);

            using var context = _world.NewContext();
            var service = _world.Cutups(context);
            await service.SetSharingAsync(headCoach, cutupId, new SetCutupSharingDto { WholeTeam = true });

            var seenByPlayer = await service.GetCutupAsync(player, cutupId);
            var seenByCoach = await service.GetCutupAsync(headCoach, cutupId);

            // The same plays in the same order...
            Assert.Equal(seenByCoach!.Plays.Select(p => p.Id), seenByPlayer!.Plays.Select(p => p.Id));
            Assert.Equal(new[] { 0, 8_000 }, seenByPlayer.Plays.Select(p => p.OffsetMs));

            // ...without the tags, the clips behind them, or who else it was shared with
            Assert.All(seenByPlayer.Plays, p => Assert.Null(p.Tag));
            Assert.All(seenByPlayer.Plays, p => Assert.Null(p.ClipId));
            Assert.Null(seenByPlayer.Sharing);
            Assert.Empty(seenByPlayer.SharedWith);
            Assert.False(seenByPlayer.CanManage);

            Assert.NotNull(seenByCoach.Plays[0].Tag);
            Assert.NotNull(seenByCoach.Sharing);
        }

        [Fact]
        public async Task OnlyCoaches_BuildAndShareCutups()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            var positionCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Linebackers");
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int cutupId = await AddCutupAsync(positionCoach, "Built by a position coach", clips[0]);

            using var context = _world.NewContext();
            var service = _world.Cutups(context);
            await service.SetSharingAsync(positionCoach, cutupId, new SetCutupSharingDto { WholeTeam = true });
            int playId = (await service.GetCutupAsync(positionCoach, cutupId))!.Plays[0].Id;

            // The player can watch it, and nothing more
            await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(player, new SaveCutupDto { Name = "Mine" }));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(player, cutupId, new SaveCutupDto { Name = "Renamed" }));
            await Assert.ThrowsAsync<ForbiddenException>(
                () => service.AddClipsAsync(player, cutupId, new AddCutupClipsDto { ClipIds = { clips[1] } }));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.MovePlayAsync(player, playId, MoveDirection.Up));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.RemovePlayAsync(player, playId));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.SetSharingAsync(player, cutupId, new SetCutupSharingDto()));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteAsync(player, cutupId));
        }

        // ---------- Deleting ----------

        [Fact]
        public async Task DeletingACutup_RemovesItsPlaysAndItsSharing_AndLeavesTheClips()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            int cutupId = await AddCutupAsync(headCoach, "Raiders", clips);

            using var context = _world.NewContext();
            var service = _world.Cutups(context);
            await service.SetSharingAsync(headCoach, cutupId, new SetCutupSharingDto { WholeTeam = true });

            Assert.True(await service.DeleteAsync(headCoach, cutupId));

            using var check = _world.NewContext();
            Assert.Equal(0, await check.CutupClips.CountAsync());
            Assert.Equal(0, await check.FilmAssignments.CountAsync());
            Assert.Equal(3, await check.Clips.CountAsync());
        }

        [Fact]
        public async Task DeletingAPositionGroup_StopsFilmBeingSharedWithIt()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            int cutupId = await AddCutupAsync(headCoach, "Raiders", clips);

            int groupId;
            using (var setup = _world.NewContext())
            {
                var group = new PositionGroup { TeamId = headCoach.TeamId, Unit = TeamUnit.Offence, Name = "Slot Receivers", SortOrder = 99 };
                setup.PositionGroups.Add(group);
                await setup.SaveChangesAsync();
                groupId = group.Id;
            }

            using var context = _world.NewContext();
            var service = _world.Cutups(context);
            await service.SetSharingAsync(headCoach, cutupId, new SetCutupSharingDto { GroupIds = { groupId } });

            Assert.True(await _world.Groups(context).DeleteGroupAsync(headCoach, groupId));

            using var check = _world.NewContext();
            Assert.Equal(0, await check.FilmAssignments.CountAsync());
            Assert.Empty((await _world.Cutups(check).GetCutupAsync(headCoach, cutupId))!.Sharing!.Groups);
        }

        // ---------- Keeping teams apart ----------

        [Fact]
        public async Task AnotherTeamsCutups_CannotBeReached()
        {
            var (headCoach, film, clips) = await TeamWithClipsAsync();
            int cutupId = await AddCutupAsync(headCoach, "Raiders", clips);
            var outsider = await _world.AddTeamAsync("Other Team");
            var theirPlayer = await _world.AddPlayerAsync(outsider, "Stranger", "Receivers");

            using var context = _world.NewContext();
            var service = _world.Cutups(context);
            await service.SetSharingAsync(headCoach, cutupId, new SetCutupSharingDto { WholeTeam = true });
            int playId = (await service.GetCutupAsync(headCoach, cutupId))!.Plays[0].Id;

            // "The whole team" means this team: a player of another team gets nothing
            Assert.Empty(await service.GetCutupsAsync(theirPlayer));
            Assert.Null(await service.GetCutupAsync(theirPlayer, cutupId));

            Assert.Empty(await service.GetCutupsAsync(outsider));
            Assert.Null(await service.GetCutupAsync(outsider, cutupId));
            Assert.False(await service.UpdateAsync(outsider, cutupId, new SaveCutupDto { Name = "Renamed" }));
            Assert.False(await service.AddClipsAsync(outsider, cutupId, new AddCutupClipsDto()));
            Assert.False(await service.MovePlayAsync(outsider, playId, MoveDirection.Down));
            Assert.False(await service.RemovePlayAsync(outsider, playId));
            Assert.False(await service.SetSharingAsync(outsider, cutupId, new SetCutupSharingDto()));
            Assert.False(await service.DeleteAsync(outsider, cutupId));

            // Nothing was changed
            var cutup = await service.GetCutupAsync(headCoach, cutupId);
            Assert.Equal("Raiders", cutup!.Name);
            Assert.Equal(3, cutup.PlayCount);
            Assert.True(cutup.Sharing!.WholeTeam);
        }
    }
}
