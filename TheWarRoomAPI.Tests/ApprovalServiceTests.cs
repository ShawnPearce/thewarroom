using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The head coach's decisions on playbook changes, and what the requester can do while waiting
    public class ApprovalServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        private async Task<int> PublishPlayAsync(CurrentUser headCoach, string name = "Slant")
        {
            using var context = _world.NewContext();
            var result = await _world.Playbooks(context).CreatePlayAsync(headCoach, new SavePlayDto
            {
                FormationId = await _world.FormationIdAsync(headCoach, "Shotgun Spread"),
                Name = name,
                DiagramJson = await _world.DiagramAsync(headCoach, "Shotgun Spread", "X"),
                Notes = "Original notes"
            });
            return result!.PlayId!.Value;
        }

        // A pending request from the given coach to rename the play and add a route for Z
        private async Task<int> RequestUpdateAsync(CurrentUser coach, int playId, string newName = "Slant and Go")
        {
            using var context = _world.NewContext();
            var result = await _world.Playbooks(context).UpdatePlayAsync(coach, playId, new SavePlayDto
            {
                Name = newName,
                DiagramJson = await _world.DiagramAsync(coach, "Shotgun Spread", "X", "Z"),
                Notes = "Proposed notes"
            });
            Assert.False(result!.Published);
            return result.RequestId;
        }

        private async Task<PlayDto> PlayAsync(CurrentUser user, int playId)
        {
            using var context = _world.NewContext();
            return (await _world.Playbooks(context).GetPlayAsync(user, playId))!;
        }

        // ---------- Approving and rejecting ----------

        [Fact]
        public async Task Approve_MakesTheChangeLive_ForPlayersToo()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);

            Assert.Equal("Slant", (await PlayAsync(player, playId)).Name);   // before the decision

            using (var context = _world.NewContext())
            {
                Assert.True(await _world.Approvals(context).ApproveAsync(headCoach, requestId, new ReviewDecisionDto { Note = "  Good change.  " }));
            }

            var seenByPlayer = await PlayAsync(player, playId);
            Assert.Equal("Slant and Go", seenByPlayer.Name);
            Assert.Equal("Proposed notes", seenByPlayer.Notes);
            Assert.Equal(2, seenByPlayer.Version);

            using var checkContext = _world.NewContext();
            var request = await _world.Approvals(checkContext).GetRequestAsync(headCoach, requestId);
            Assert.Equal(ApprovalStatus.Approved, request!.Status);
            Assert.False(request.AutoApproved);
            Assert.Equal("Head Coach", request.ReviewedBy);
            Assert.Equal("Good change.", request.ReviewNote);

            // The history keeps what was replaced
            Assert.Equal("Slant", request.BeforeName);
            Assert.Equal("Original notes", request.BeforeNotes);
            Assert.NotEqual(request.BeforeJson, request.ProposedJson);
        }

        [Fact]
        public async Task Approve_ANewPlay_AddsItUnderItsFormation()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            int formationId = await _world.FormationIdAsync(headCoach, "Shotgun Spread");

            int requestId;
            using (var context = _world.NewContext())
            {
                var result = await _world.Playbooks(context).CreatePlayAsync(receiversCoach, new SavePlayDto
                {
                    FormationId = formationId,
                    Name = "Mesh",
                    DiagramJson = await _world.DiagramAsync(headCoach, "Shotgun Spread", "H", "Y")
                });
                requestId = result!.RequestId;
            }

            using (var context = _world.NewContext())
            {
                // Before the decision the request is compared with the formation it is drawn on
                var waiting = await _world.Approvals(context).GetRequestAsync(headCoach, requestId);
                Assert.Equal("Shotgun Spread (formation)", waiting!.BeforeName);
                Assert.True(waiting.CanReview);

                Assert.True(await _world.Approvals(context).ApproveAsync(headCoach, requestId, new ReviewDecisionDto()));
            }

            using var checkContext = _world.NewContext();
            var formation = await _world.Playbooks(checkContext).GetFormationAsync(headCoach, formationId);
            Assert.Equal("Mesh", Assert.Single(formation!.Plays).Name);
        }

        [Fact]
        public async Task Reject_LeavesTheApprovedVersionAsItIs()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);

            using (var context = _world.NewContext())
            {
                Assert.True(await _world.Approvals(context).RejectAsync(headCoach, requestId, new ReviewDecisionDto { Note = "Not this week." }));
            }

            var play = await PlayAsync(headCoach, playId);
            Assert.Equal("Slant", play.Name);
            Assert.Equal(1, play.Version);
            Assert.Null(play.PendingRequestId);   // nothing is waiting any more

            // A decided request cannot be decided again
            using var againContext = _world.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Approvals(againContext).ApproveAsync(headCoach, requestId, new ReviewDecisionDto()));
        }

        [Fact]
        public async Task OnlyTheHeadCoach_MayApproveOrReject()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var offenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);

            using var context = _world.NewContext();
            var service = _world.Approvals(context);

            await Assert.ThrowsAsync<ForbiddenException>(() => service.ApproveAsync(offenceCoordinator, requestId, new ReviewDecisionDto()));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.RejectAsync(offenceCoordinator, requestId, new ReviewDecisionDto()));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.ApproveAsync(receiversCoach, requestId, new ReviewDecisionDto()));   // not even their own

            Assert.Equal("Slant", (await PlayAsync(headCoach, playId)).Name);
        }

        [Fact]
        public async Task WhileAChangeIsWaiting_NobodyElseCanChangeThatPlay_NotEvenTheHeadCoach()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            int playId = await PublishPlayAsync(headCoach);
            await RequestUpdateAsync(receiversCoach, playId);
            var headCoachsVersion = new SavePlayDto
            {
                Name = "Head coach's version",
                DiagramJson = await _world.DiagramAsync(headCoach, "Shotgun Spread", "H")
            };

            // The head coach decides on the waiting request first; a direct edit on top of it is refused
            using var context = _world.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Playbooks(context).UpdatePlayAsync(headCoach, playId, headCoachsVersion));
        }

        [Fact]
        public async Task Approve_ARequestMadeAgainstAnOlderVersion_IsRefused()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);

            // The play moves on to a newer version after the request was made
            using (var context = _world.NewContext())
            {
                var play = await context.Plays.SingleAsync(p => p.Id == playId);
                play.Version = 5;
                await context.SaveChangesAsync();
            }

            using var approveContext = _world.NewContext();
            var service = _world.Approvals(approveContext);

            Assert.True((await service.GetRequestAsync(headCoach, requestId))!.IsStale);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(headCoach, requestId, new ReviewDecisionDto()));
            Assert.Contains("has changed since", error.Message);

            // The request stays waiting (it can still be rejected or withdrawn) and the play is untouched
            Assert.Equal(ApprovalStatus.Pending, (await service.GetRequestAsync(headCoach, requestId))!.Status);
            Assert.Equal("Slant", (await PlayAsync(headCoach, playId)).Name);
        }

        [Fact]
        public async Task GrantingThePublishRightLater_DoesNotPublishWhatIsAlreadyWaiting()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);

            using (var context = _world.NewContext())
            {
                await _world.Users(context).UpdatePermissionsAsync(headCoach, receiversCoach.Id,
                    new UpdatePermissionsDto { CanPublishPlaybook = true, CanEditDepthChart = true, CanRecordAttendance = true });
            }

            using var checkContext = _world.NewContext();
            Assert.Equal(ApprovalStatus.Pending, (await _world.Approvals(checkContext).GetRequestAsync(headCoach, requestId))!.Status);
            Assert.Equal("Slant", (await PlayAsync(headCoach, playId)).Name);
        }

        // ---------- The requester, while waiting ----------

        [Fact]
        public async Task TheRequester_CanReviseTheirPendingRequest_AndOthersCannot()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var offenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);
            var revision = new ReviseApprovalRequestDto
            {
                Name = "Slant Return",
                DiagramJson = await _world.DiagramAsync(headCoach, "Shotgun Spread", "X", "Z", "H"),
                Notes = "Third look"
            };

            using var context = _world.NewContext();
            var service = _world.Approvals(context);

            await Assert.ThrowsAsync<ForbiddenException>(() => service.ReviseAsync(offenceCoordinator, requestId, revision));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.ReviseAsync(headCoach, requestId, revision));
            Assert.True(await service.ReviseAsync(receiversCoach, requestId, revision));

            var request = await service.GetRequestAsync(receiversCoach, requestId);
            Assert.Equal("Slant Return", request!.ProposedName);
            Assert.Equal("Third look", request.ProposedNotes);
            Assert.Equal(ApprovalStatus.Pending, request.Status);
            Assert.True(request.CanRevise);
            Assert.False(request.CanReview);

            // Still nothing is live
            Assert.Equal("Slant", (await PlayAsync(headCoach, playId)).Name);
        }

        [Fact]
        public async Task TheRequester_CanWithdraw_AndThenTheTargetIsFreeForAnotherChange()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var offenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);

            using (var context = _world.NewContext())
            {
                var service = _world.Approvals(context);
                await Assert.ThrowsAsync<ForbiddenException>(() => service.WithdrawAsync(offenceCoordinator, requestId));
                Assert.True(await service.WithdrawAsync(receiversCoach, requestId));

                // Withdrawn means decided: it can no longer be approved or revised
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(headCoach, requestId, new ReviewDecisionDto()));
            }

            // Another coach can now propose a change to the same play
            int secondRequestId = await RequestUpdateAsync(offenceCoordinator, playId, "Coordinator's version");
            Assert.NotEqual(requestId, secondRequestId);
        }

        // ---------- Who sees which requests ----------

        [Fact]
        public async Task Requests_AreSeenByTheHeadCoach_TheRequester_AndCoachesOfThatPlaybook()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var lineCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Offensive Line");
            var defenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Defence);
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);

            using var context = _world.NewContext();
            var service = _world.Approvals(context);

            // The head coach also sees their own direct publish in the history
            Assert.Equal(2, (await service.GetRequestsAsync(headCoach, null)).Count);
            Assert.Equal(requestId, Assert.Single(await service.GetRequestsAsync(headCoach, ApprovalStatus.Pending)).Id);

            Assert.Single(await service.GetRequestsAsync(receiversCoach, ApprovalStatus.Pending));
            Assert.Single(await service.GetRequestsAsync(lineCoach, ApprovalStatus.Pending));          // same playbook
            Assert.Empty(await service.GetRequestsAsync(defenceCoordinator, ApprovalStatus.Pending));   // another playbook
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetRequestAsync(defenceCoordinator, requestId));

            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetRequestsAsync(player, null));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetRequestAsync(player, requestId));
        }

        [Fact]
        public async Task AnotherTeamsRequests_CannotBeReached()
        {
            var tigersCoach = await _world.AddTeamAsync("Test Tigers");
            var tigersAssistant = await _world.AddCoachAsync(tigersCoach, UserRole.PositionCoach, null, "Receivers");
            int playId = await PublishPlayAsync(tigersCoach);
            int requestId = await RequestUpdateAsync(tigersAssistant, playId);
            var bearsCoach = await _world.AddTeamAsync("Test Bears");

            using var context = _world.NewContext();
            var service = _world.Approvals(context);

            Assert.Empty(await service.GetRequestsAsync(bearsCoach, null));
            Assert.Null(await service.GetRequestAsync(bearsCoach, requestId));
            Assert.False(await service.ApproveAsync(bearsCoach, requestId, new ReviewDecisionDto()));
            Assert.False(await service.RejectAsync(bearsCoach, requestId, new ReviewDecisionDto()));
            Assert.False(await service.WithdrawAsync(bearsCoach, requestId));

            Assert.Equal(ApprovalStatus.Pending, (await service.GetRequestAsync(tigersCoach, requestId))!.Status);
        }
    }
}
