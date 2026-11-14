using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The playbook: who may read it, who may change it, and whether a change is published or waits for approval
    public class PlaybookServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        // Adds a play under Shotgun Spread as the given coach and returns what happened to the change
        private async Task<PlaybookChangeResultDto> AddPlayAsync(CurrentUser coach, string name = "Slant", string formation = "Shotgun Spread", params string[] routes)
        {
            using var context = _world.NewContext();
            var result = await _world.Playbooks(context).CreatePlayAsync(coach, new SavePlayDto
            {
                FormationId = await _world.FormationIdAsync(coach, formation),
                Name = name,
                DiagramJson = await _world.DiagramAsync(coach, formation, routes.Length > 0 ? routes : new[] { "X" }),
                Notes = "Quick game"
            });
            return result!;
        }

        private async Task<PlaybookChangeResultDto> UpdatePlayAsync(CurrentUser coach, int playId, string name, params string[] routes)
        {
            using var context = _world.NewContext();
            var result = await _world.Playbooks(context).UpdatePlayAsync(coach, playId, new SavePlayDto
            {
                Name = name,
                DiagramJson = await _world.DiagramAsync(coach, "Shotgun Spread", routes),
                Notes = "Changed"
            });
            return result!;
        }

        // ---------- What a team starts with, and who may read it ----------

        [Fact]
        public async Task ANewTeam_HasAPlaybookPerUnit_WithTheBaseFormations()
        {
            var headCoach = await _world.AddTeamAsync();

            using var context = _world.NewContext();
            var playbooks = await _world.Playbooks(context).GetPlaybooksAsync(headCoach);

            Assert.Equal(new[] { TeamUnit.Offence, TeamUnit.Defence, TeamUnit.SpecialTeams }, playbooks.Select(p => p.Unit));
            Assert.Equal(new[] { 5, 3, 5 }, playbooks.Select(p => p.FormationCount));
            Assert.All(playbooks, p => Assert.True(p.CanChange && p.CanPublish));
        }

        [Fact]
        public async Task APlayer_ReadsOnlyThePlaybooksOfTheirGroupsUnits()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiver = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var twoWayPlayer = await _world.AddPlayerAsync(headCoach, "Kim", "Receivers", "Defensive Backs");
            int defencePlaybook = await _world.PlaybookIdAsync(headCoach, TeamUnit.Defence);
            int defenceFormation = await _world.FormationIdAsync(headCoach, "4-3");

            using var context = _world.NewContext();
            var service = _world.Playbooks(context);

            var seenByReceiver = await service.GetPlaybooksAsync(receiver);
            Assert.Equal(TeamUnit.Offence, Assert.Single(seenByReceiver).Unit);
            Assert.False(seenByReceiver[0].CanChange);

            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetPlaybookAsync(receiver, defencePlaybook));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetFormationAsync(receiver, defenceFormation));

            // A player in groups on both sides of the ball reads both playbooks
            Assert.Equal(2, (await service.GetPlaybooksAsync(twoWayPlayer)).Count);
            Assert.NotNull(await service.GetFormationAsync(twoWayPlayer, defenceFormation));
        }

        // ---------- Approval by default ----------

        [Fact]
        public async Task ACoachWithoutThePublishRight_HasTheirChangeQueued_AndPlayersKeepSeeingTheApprovedVersion()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var published = await AddPlayAsync(headCoach, "Slant", "Shotgun Spread", "X");
            int playId = published.PlayId!.Value;

            var change = await UpdatePlayAsync(receiversCoach, playId, "Slant and Go", "X", "Z");

            Assert.False(change.Published);

            using var context = _world.NewContext();
            var service = _world.Playbooks(context);

            // The player still gets the approved version, and is not told about the pending change
            var seenByPlayer = await service.GetPlayAsync(player, playId);
            Assert.Equal("Slant", seenByPlayer!.Name);
            Assert.Equal(1, seenByPlayer.Version);
            Assert.Equal(published.PlayId, seenByPlayer.Id);
            Assert.Null(seenByPlayer.PendingRequestId);
            Assert.Equal("Quick game", seenByPlayer.Notes);

            // Coaches see the same approved content, plus a pointer to the request that is waiting
            var seenByCoach = await service.GetPlayAsync(receiversCoach, playId);
            Assert.Equal("Slant", seenByCoach!.Name);
            Assert.Equal(change.RequestId, seenByCoach.PendingRequestId);
        }

        [Fact]
        public async Task ANewPlayFromACoachWithoutThePublishRight_DoesNotExistUntilItIsApproved()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            int formationId = await _world.FormationIdAsync(headCoach, "Shotgun Spread");

            var change = await AddPlayAsync(receiversCoach, "Mesh");

            Assert.False(change.Published);
            Assert.Null(change.PlayId);

            using var context = _world.NewContext();
            Assert.Empty((await _world.Playbooks(context).GetFormationAsync(headCoach, formationId))!.Plays);
            Assert.Equal(0, await context.Plays.CountAsync());
        }

        [Fact]
        public async Task TheHeadCoach_PublishesAtOnce_AndTheVersionGoesUp()
        {
            var headCoach = await _world.AddTeamAsync();
            var created = await AddPlayAsync(headCoach, "Slant");

            var change = await UpdatePlayAsync(headCoach, created.PlayId!.Value, "Slant and Go", "X", "Z");

            Assert.True(created.Published);
            Assert.True(change.Published);

            using var context = _world.NewContext();
            var play = await _world.Playbooks(context).GetPlayAsync(headCoach, created.PlayId.Value);
            Assert.Equal("Slant and Go", play!.Name);
            Assert.Equal(2, play.Version);
            Assert.Equal("Changed", play.Notes);

            // Even a direct publish leaves a record of who changed what
            var history = await context.ApprovalRequests.OrderBy(r => r.Id).ToListAsync();
            Assert.Equal(2, history.Count);
            Assert.All(history, r => Assert.True(r.AutoApproved && r.Status == ApprovalStatus.Approved));
            Assert.Equal("Slant", history[1].PreviousName);
        }

        // ---------- The publish right, and the playbooks a coach may change ----------

        [Fact]
        public async Task ACoachGrantedThePublishRight_PublishesInsideTheirScope_AndIsRefusedOutsideIt()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            using (var context = _world.NewContext())
            {
                await _world.Users(context).UpdatePermissionsAsync(headCoach, receiversCoach.Id,
                    new UpdatePermissionsDto { CanPublishPlaybook = true, CanEditDepthChart = true, CanRecordAttendance = true });
            }
            var withPublishRight = await _world.AsAsync(receiversCoach.Id);

            // Receivers are an offensive group, so the offence playbook is in scope
            var onOffence = await AddPlayAsync(withPublishRight, "Slant", "Shotgun Spread", "X");
            Assert.True(onOffence.Published);

            // The defence playbook is not, whatever the publish right says
            var defencePlay = new SavePlayDto
            {
                FormationId = await _world.FormationIdAsync(headCoach, "4-3"),
                Name = "Cover 3",
                DiagramJson = await _world.DiagramAsync(headCoach, "4-3", "FS")
            };
            using var defenceContext = _world.NewContext();
            await Assert.ThrowsAsync<ForbiddenException>(
                () => _world.Playbooks(defenceContext).CreatePlayAsync(withPublishRight, defencePlay));
        }

        [Fact]
        public async Task ACoordinator_MayChangeTheirOwnUnitsPlaybook_ButNotAnother()
        {
            var headCoach = await _world.AddTeamAsync();
            var defenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Defence);

            using var context = _world.NewContext();
            var service = _world.Playbooks(context);

            var onDefence = await service.CreatePlayAsync(defenceCoordinator, new SavePlayDto
            {
                FormationId = await _world.FormationIdAsync(headCoach, "4-3"),
                Name = "Cover 3",
                DiagramJson = await _world.DiagramAsync(headCoach, "4-3", "FS")
            });
            Assert.False(onDefence!.Published);   // in scope, but still needs approval

            await Assert.ThrowsAsync<ForbiddenException>(() => AddPlayAsync(defenceCoordinator, "Slant"));

            var playbooks = await service.GetPlaybooksAsync(defenceCoordinator);
            Assert.True(playbooks.Single(p => p.Unit == TeamUnit.Defence).CanChange);
            Assert.False(playbooks.Single(p => p.Unit == TeamUnit.Offence).CanChange);
            Assert.All(playbooks, p => Assert.False(p.CanPublish));
        }

        [Fact]
        public async Task APlayer_CannotChangeThePlaybook()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var created = await AddPlayAsync(headCoach, "Slant");

            await Assert.ThrowsAsync<ForbiddenException>(() => AddPlayAsync(player, "My own play"));
            await Assert.ThrowsAsync<ForbiddenException>(() => UpdatePlayAsync(player, created.PlayId!.Value, "Renamed", "X"));

            using var context = _world.NewContext();
            await Assert.ThrowsAsync<ForbiddenException>(() => _world.Playbooks(context).DeletePlayAsync(player, created.PlayId!.Value));
        }

        // ---------- Rules about the content ----------

        [Fact]
        public async Task OnlyOneChangeAtATime_MayBeWaitingOnAPlay()
        {
            var headCoach = await _world.AddTeamAsync();
            var firstCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var secondCoach = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            var created = await AddPlayAsync(headCoach, "Slant");
            await UpdatePlayAsync(firstCoach, created.PlayId!.Value, "First idea", "X");

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => UpdatePlayAsync(secondCoach, created.PlayId.Value, "Second idea", "Z"));

            Assert.Contains("already waiting for approval", error.Message);
        }

        [Fact]
        public async Task SavingFromADesignerOpenedBeforeSomeoneElsesChange_IsRefused()
        {
            var headCoach = await _world.AddTeamAsync();
            var created = await AddPlayAsync(headCoach, "Slant");
            int playId = created.PlayId!.Value;

            // A designer is open on version 1 when the head coach publishes version 2
            await UpdatePlayAsync(headCoach, playId, "Slant", "Z");
            var drawnOnVersionOne = new SavePlayDto
            {
                Name = "Slant",
                DiagramJson = await _world.DiagramAsync(headCoach, "Shotgun Spread", "X"),
                Version = 1
            };

            using var context = _world.NewContext();
            var service = _world.Playbooks(context);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdatePlayAsync(headCoach, playId, drawnOnVersionOne));
            Assert.Contains("changed by someone else", error.Message);

            // Starting again from the current version works
            drawnOnVersionOne.Version = 2;
            var result = await service.UpdatePlayAsync(headCoach, playId, drawnOnVersionOne);
            Assert.True(result!.Published);
        }

        [Fact]
        public async Task Names_MustBeUniqueWithinAFormationAndWithinAPlaybook()
        {
            var headCoach = await _world.AddTeamAsync();
            await AddPlayAsync(headCoach, "Slant");

            // The same play name under the same formation, in different capitals
            await Assert.ThrowsAsync<InvalidOperationException>(() => AddPlayAsync(headCoach, "SLANT"));

            // ...but another formation may have its own "Slant"
            var elsewhere = await AddPlayAsync(headCoach, "Slant", "I-Form", "X");
            Assert.True(elsewhere.Published);

            // A formation name is unique within its playbook, again without regard to capitals
            var duplicateFormation = new SaveFormationDto
            {
                PlaybookId = await _world.PlaybookIdAsync(headCoach, TeamUnit.Offence),
                Name = "i-form",
                DiagramJson = await _world.DiagramAsync(headCoach, "I-Form")
            };
            using var context = _world.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Playbooks(context).CreateFormationAsync(headCoach, duplicateFormation));
        }

        [Fact]
        public async Task SavingAFormationUnderANewName_AddsItToThePlaybook_WithoutRoutes()
        {
            var headCoach = await _world.AddTeamAsync();
            int playbookId = await _world.PlaybookIdAsync(headCoach, TeamUnit.Offence);

            using var context = _world.NewContext();
            var service = _world.Playbooks(context);

            var result = await service.CreateFormationAsync(headCoach, new SaveFormationDto
            {
                PlaybookId = playbookId,
                Name = "Spread Tight",
                DiagramJson = await _world.DiagramAsync(headCoach, "Shotgun Spread", "X")   // a route sneaks in
            });

            var formation = await service.GetFormationAsync(headCoach, result!.FormationId!.Value);
            Assert.Equal("Spread Tight", formation!.Name);
            Assert.DoesNotContain("route", formation.DiagramJson);   // a formation is players only
            Assert.Equal(6, (await service.GetPlaybookAsync(headCoach, playbookId))!.Formations.Count);
        }

        [Fact]
        public async Task AFormation_CanBeRemovedOnlyWhenItHasNoPlays()
        {
            var headCoach = await _world.AddTeamAsync();
            await AddPlayAsync(headCoach, "Slant");
            int withPlays = await _world.FormationIdAsync(headCoach, "Shotgun Spread");
            int empty = await _world.FormationIdAsync(headCoach, "Pistol");

            using var context = _world.NewContext();
            var service = _world.Playbooks(context);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteFormationAsync(headCoach, withPlays));
            Assert.Contains("still has plays", error.Message);

            var removed = await service.DeleteFormationAsync(headCoach, empty);
            Assert.True(removed!.Published);
            Assert.Null(await service.GetFormationAsync(headCoach, empty));
        }

        [Fact]
        public async Task ADiagramThatBreaksTheLimits_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync();

            var broken = new SavePlayDto
            {
                FormationId = await _world.FormationIdAsync(headCoach, "Shotgun Spread"),
                Name = "Broken",
                DiagramJson = "{ this is not a diagram }"
            };
            using var context = _world.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _world.Playbooks(context).CreatePlayAsync(headCoach, broken));

            Assert.Equal(0, await context.Plays.CountAsync());
            Assert.Equal(0, await context.ApprovalRequests.CountAsync());
        }

        // ---------- Keeping teams apart ----------

        [Fact]
        public async Task AnotherTeamsPlaybook_CannotBeReached()
        {
            var tigersCoach = await _world.AddTeamAsync("Test Tigers");
            var tigersPlay = await AddPlayAsync(tigersCoach, "Slant");
            int tigersPlaybook = await _world.PlaybookIdAsync(tigersCoach, TeamUnit.Offence);
            int tigersFormation = await _world.FormationIdAsync(tigersCoach, "Shotgun Spread");
            var bearsCoach = await _world.AddTeamAsync("Test Bears");
            string diagram = await _world.DiagramAsync(bearsCoach, "Shotgun Spread", "X");

            using var context = _world.NewContext();
            var service = _world.Playbooks(context);

            Assert.Null(await service.GetPlaybookAsync(bearsCoach, tigersPlaybook));
            Assert.Null(await service.GetFormationAsync(bearsCoach, tigersFormation));
            Assert.Null(await service.GetPlayAsync(bearsCoach, tigersPlay.PlayId!.Value));
            Assert.Null(await service.UpdatePlayAsync(bearsCoach, tigersPlay.PlayId.Value, new SavePlayDto { Name = "Taken", DiagramJson = diagram }));
            Assert.Null(await service.DeletePlayAsync(bearsCoach, tigersPlay.PlayId.Value));
            Assert.Null(await service.CreatePlayAsync(bearsCoach, new SavePlayDto { FormationId = tigersFormation, Name = "Planted", DiagramJson = diagram }));
            Assert.Null(await service.UpdateFormationAsync(bearsCoach, tigersFormation, new SaveFormationDto { Name = "Taken", DiagramJson = diagram }));
            Assert.Null(await service.DeleteFormationAsync(bearsCoach, tigersFormation));
            Assert.Null(await service.CreateFormationAsync(bearsCoach, new SaveFormationDto { PlaybookId = tigersPlaybook, Name = "Planted", DiagramJson = diagram }));

            Assert.Equal("Slant", (await service.GetPlayAsync(tigersCoach, tigersPlay.PlayId.Value))!.Name);
        }
    }
}
