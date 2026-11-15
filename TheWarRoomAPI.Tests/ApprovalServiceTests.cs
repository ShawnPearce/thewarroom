using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The head coach's decisions on playbook changes, and what the requester can do while waiting
    // and after a rejection
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

        private async Task RejectAsync(CurrentUser headCoach, int requestId, string? note = "Not this week.")
        {
            using var context = _world.NewContext();
            Assert.True(await _world.Approvals(context).RejectAsync(headCoach, requestId, new ReviewDecisionDto { Note = note }));
        }

        // What the coach draws after reading the head coach's note
        private async Task<ReviseApprovalRequestDto> RevisedDrawingAsync(CurrentUser coach, string name = "Slant Return")
        {
            return new ReviseApprovalRequestDto
            {
                Name = name,
                DiagramJson = await _world.DiagramAsync(coach, "Shotgun Spread", "X", "Z", "H"),
                Notes = "After the head coach's note"
            };
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

        // ---------- The requester, after a rejection ----------

        [Fact]
        public async Task ARejection_IsNewsToTheCoachWhoAsked_UntilTheyOpenIt()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var lineCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Offensive Line");
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);

            // While the change is waiting, the head coach is the one who is told
            using (var context = _world.NewContext())
            {
                var service = _world.Approvals(context);
                Assert.Equal(1, (await service.GetNoticesAsync(headCoach)).WaitingForDecision);
                Assert.Equal(0, (await service.GetNoticesAsync(receiversCoach)).WaitingForDecision);
                Assert.Equal(0, (await service.GetNoticesAsync(receiversCoach)).SentBack);
            }

            await RejectAsync(headCoach, requestId, "Run it from trips instead.");

            using (var context = _world.NewContext())
            {
                var service = _world.Approvals(context);

                // Now the coach who asked is told, and nobody else
                Assert.Equal(0, (await service.GetNoticesAsync(headCoach)).WaitingForDecision);
                Assert.Equal(0, (await service.GetNoticesAsync(headCoach)).SentBack);
                Assert.Equal(1, (await service.GetNoticesAsync(receiversCoach)).SentBack);
                Assert.Equal(0, (await service.GetNoticesAsync(lineCoach)).SentBack);
                await Assert.ThrowsAsync<ForbiddenException>(() => service.GetNoticesAsync(player));

                var mine = await service.GetRequestAsync(receiversCoach, requestId);
                Assert.True(mine!.IsNewDecision);
                Assert.True(mine.CanResubmit);
                Assert.Equal("Run it from trips instead.", mine.ReviewNote);

                var seenByOthers = await service.GetRequestAsync(lineCoach, requestId);
                Assert.False(seenByOthers!.IsNewDecision);
                Assert.False(seenByOthers.CanResubmit);
                Assert.False((await service.GetRequestAsync(headCoach, requestId))!.CanResubmit);

                // Only the coach who asked can mark it as read. Reading it twice changes nothing.
                await Assert.ThrowsAsync<ForbiddenException>(() => service.MarkSeenAsync(lineCoach, requestId));
                await Assert.ThrowsAsync<ForbiddenException>(() => service.MarkSeenAsync(headCoach, requestId));
                Assert.True(await service.MarkSeenAsync(receiversCoach, requestId));
                Assert.True(await service.MarkSeenAsync(receiversCoach, requestId));
            }

            using var checkContext = _world.NewContext();
            var check = _world.Approvals(checkContext);

            Assert.Equal(0, (await check.GetNoticesAsync(receiversCoach)).SentBack);

            // It is no longer news, but it can still be revised and sent again
            var read = await check.GetRequestAsync(receiversCoach, requestId);
            Assert.False(read!.IsNewDecision);
            Assert.True(read.CanResubmit);
            Assert.True(Assert.Single(await check.GetRequestsAsync(receiversCoach, ApprovalStatus.Rejected)).CanResubmit);
        }

        [Fact]
        public async Task ARejectedChange_CanBeRevisedAndSentAgain_AsANewRequestThatPointsBack()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);
            await RejectAsync(headCoach, requestId, "Bring Z across the middle.");

            int newRequestId;
            using (var context = _world.NewContext())
            {
                var result = await _world.Approvals(context).ResubmitAsync(receiversCoach, requestId, await RevisedDrawingAsync(receiversCoach));

                Assert.False(result!.Published);
                Assert.Equal(playId, result.PlayId);
                newRequestId = result.RequestId;
                Assert.NotEqual(requestId, newRequestId);
            }

            using (var context = _world.NewContext())
            {
                var service = _world.Approvals(context);

                // The new request is waiting, and carries what the head coach said about the first one
                var again = await service.GetRequestAsync(headCoach, newRequestId);
                Assert.Equal(ApprovalStatus.Pending, again!.Status);
                Assert.Equal(requestId, again.ResubmittedFromId);
                Assert.Equal("Bring Z across the middle.", again.EarlierReviewNote);
                Assert.NotNull(again.EarlierReviewedBy);
                Assert.NotNull(again.EarlierReviewedAt);
                Assert.Equal("Slant Return", again.ProposedName);
                Assert.Equal("After the head coach's note", again.ProposedNotes);
                Assert.True(again.CanReview);
                Assert.Equal(1, (await service.GetNoticesAsync(headCoach)).WaitingForDecision);

                // The rejected request stays in the history as it was, and points at the request that took its place
                var first = await service.GetRequestAsync(receiversCoach, requestId);
                Assert.Equal(ApprovalStatus.Rejected, first!.Status);
                Assert.Equal("Slant and Go", first.ProposedName);
                Assert.Equal(newRequestId, first.ResubmittedAsId);
                Assert.False(first.CanResubmit);
                Assert.False(first.IsNewDecision);   // sending it again means the rejection was read
                Assert.Equal(0, (await service.GetNoticesAsync(receiversCoach)).SentBack);

                // Nothing is live until the head coach approves
                Assert.Equal("Slant", (await PlayAsync(headCoach, playId)).Name);
                Assert.True(await service.ApproveAsync(headCoach, newRequestId, new ReviewDecisionDto()));
            }

            var play = await PlayAsync(headCoach, playId);
            Assert.Equal("Slant Return", play.Name);
            Assert.Equal(2, play.Version);
        }

        [Fact]
        public async Task ARejectedNewPlay_IsSentAgain_UnderTheSameFormation()
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
            await RejectAsync(headCoach, requestId, "The crossers meet at the same depth.");

            int newRequestId;
            using (var context = _world.NewContext())
            {
                var result = await _world.Approvals(context).ResubmitAsync(receiversCoach, requestId, await RevisedDrawingAsync(receiversCoach, "Mesh Return"));
                newRequestId = result!.RequestId;
                Assert.False(result.Published);
                Assert.Null(result.PlayId);   // the play is still only a proposal
            }

            using (var context = _world.NewContext())
            {
                var service = _world.Approvals(context);

                var again = await service.GetRequestAsync(headCoach, newRequestId);
                Assert.Equal(PlaybookChangeAction.Create, again!.Action);
                Assert.Equal(PlaybookTargetType.Play, again.TargetType);
                Assert.Equal(formationId, again.FormationId);
                Assert.Equal("Mesh Return", again.TargetName);
                Assert.Equal("Shotgun Spread (formation)", again.BeforeName);

                Assert.True(await service.ApproveAsync(headCoach, newRequestId, new ReviewDecisionDto()));
            }

            using var checkContext = _world.NewContext();
            var formation = await _world.Playbooks(checkContext).GetFormationAsync(headCoach, formationId);
            Assert.Equal("Mesh Return", Assert.Single(formation!.Plays).Name);
        }

        [Fact]
        public async Task OnlyTheCoachWhoAsked_CanSendItAgain_AndOnlyOnce()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var offenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);
            await RejectAsync(headCoach, requestId);
            var revised = await RevisedDrawingAsync(receiversCoach);

            using var context = _world.NewContext();
            var service = _world.Approvals(context);

            await Assert.ThrowsAsync<ForbiddenException>(() => service.ResubmitAsync(offenceCoordinator, requestId, revised));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.ResubmitAsync(headCoach, requestId, revised));

            int newRequestId = (await service.ResubmitAsync(receiversCoach, requestId, revised))!.RequestId;

            // From now on the newer request is the one to work on
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResubmitAsync(receiversCoach, requestId, revised));
            Assert.Contains("already been sent", error.Message);
            Assert.False((await service.GetRequestAsync(receiversCoach, requestId))!.CanResubmit);

            // Taking the newer request back frees the rejected one to be sent again
            Assert.True(await service.WithdrawAsync(receiversCoach, newRequestId));
            var first = await service.GetRequestAsync(receiversCoach, requestId);
            Assert.True(first!.CanResubmit);
            Assert.Null(first.ResubmittedAsId);
            Assert.NotNull(await service.ResubmitAsync(receiversCoach, requestId, revised));
        }

        [Fact]
        public async Task OnlyARejectedChangeWithADrawing_CanBeSentAgain()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            int playId = await PublishPlayAsync(headCoach);
            int otherPlayId = await PublishPlayAsync(headCoach, "Hitch");
            int requestId = await RequestUpdateAsync(receiversCoach, playId);
            var revised = await RevisedDrawingAsync(receiversCoach);

            // A request to delete, which the head coach turns down
            int deleteRequestId;
            using (var context = _world.NewContext())
            {
                deleteRequestId = (await _world.Playbooks(context).DeletePlayAsync(receiversCoach, otherPlayId))!.RequestId;
            }
            await RejectAsync(headCoach, deleteRequestId, "We still run it.");

            using var checkContext = _world.NewContext();
            var service = _world.Approvals(checkContext);

            // Still waiting: it is revised, not sent again
            var waiting = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResubmitAsync(receiversCoach, requestId, revised));
            Assert.Contains("Revise it instead", waiting.Message);
            Assert.False((await service.GetRequestAsync(receiversCoach, requestId))!.CanResubmit);

            // Approved: the change is live, so there is nothing to send again
            Assert.True(await service.ApproveAsync(headCoach, requestId, new ReviewDecisionDto()));
            var approved = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResubmitAsync(receiversCoach, requestId, revised));
            Assert.Contains("approved", approved.Message);

            // A rejected delete has no drawing to revise. The coach is still told about it.
            var delete = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResubmitAsync(receiversCoach, deleteRequestId, revised));
            Assert.Contains("nothing to revise", delete.Message);
            var rejectedDelete = await service.GetRequestAsync(receiversCoach, deleteRequestId);
            Assert.False(rejectedDelete!.CanResubmit);
            Assert.True(rejectedDelete.IsNewDecision);
            Assert.Equal(1, (await service.GetNoticesAsync(receiversCoach)).SentBack);
        }

        [Fact]
        public async Task SendingAgain_IsRefused_WhenThePlayHasChangedSince()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);
            await RejectAsync(headCoach, requestId);

            // After the rejection somebody else's change to the play goes live
            using (var context = _world.NewContext())
            {
                var changed = await _world.Playbooks(context).UpdatePlayAsync(headCoach, playId, new SavePlayDto
                {
                    Name = "Slant",
                    DiagramJson = await _world.DiagramAsync(headCoach, "Shotgun Spread", "X", "Y")
                });
                Assert.True(changed!.Published);
            }

            using var checkContext = _world.NewContext();
            var service = _world.Approvals(checkContext);

            // The coach's drawing was made on the older version, so it is drawn again on the current one instead
            var request = await service.GetRequestAsync(receiversCoach, requestId);
            Assert.True(request!.IsStale);
            Assert.False(request.CanResubmit);

            var revised = await RevisedDrawingAsync(receiversCoach);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResubmitAsync(receiversCoach, requestId, revised));
            Assert.Contains("has changed since", error.Message);
            Assert.Equal(2, (await PlayAsync(headCoach, playId)).Version);
        }

        [Fact]
        public async Task ACoachWhoMayPublishByNow_PublishesWhenSendingAgain()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            int playId = await PublishPlayAsync(headCoach);
            int requestId = await RequestUpdateAsync(receiversCoach, playId);
            await RejectAsync(headCoach, requestId);

            // After the rejection the head coach gives this coach the right to publish
            using (var context = _world.NewContext())
            {
                await _world.Users(context).UpdatePermissionsAsync(headCoach, receiversCoach.Id,
                    new UpdatePermissionsDto { CanPublishPlaybook = true, CanEditDepthChart = true, CanRecordAttendance = true });
            }
            receiversCoach = await _world.AsAsync(receiversCoach.Id);

            using var sendContext = _world.NewContext();
            var service = _world.Approvals(sendContext);

            Assert.True((await service.GetRequestAsync(receiversCoach, requestId))!.CanPublish);

            var result = await service.ResubmitAsync(receiversCoach, requestId, await RevisedDrawingAsync(receiversCoach));
            Assert.True(result!.Published);

            // It went live through the same path as any direct publish, and is kept in the history as one
            var published = await service.GetRequestAsync(headCoach, result.RequestId);
            Assert.Equal(ApprovalStatus.Approved, published!.Status);
            Assert.True(published.AutoApproved);
            Assert.Equal(requestId, published.ResubmittedFromId);
            Assert.Equal("Slant Return", (await PlayAsync(headCoach, playId)).Name);
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
            Assert.False(await service.MarkSeenAsync(bearsCoach, requestId));
            Assert.Null(await service.ResubmitAsync(bearsCoach, requestId, await RevisedDrawingAsync(tigersCoach)));
            Assert.Equal(0, (await service.GetNoticesAsync(bearsCoach)).WaitingForDecision);

            Assert.Equal(ApprovalStatus.Pending, (await service.GetRequestAsync(tigersCoach, requestId))!.Status);
        }
    }
}
