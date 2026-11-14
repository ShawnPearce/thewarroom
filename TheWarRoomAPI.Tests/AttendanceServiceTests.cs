using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Attendance: only permitted coaches may record it, and only while the head coach has tracking switched on
    public class AttendanceServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        private static RecordAttendanceDto Sheet(params (CurrentUser Player, AttendanceStatus? Status)[] entries)
        {
            return new RecordAttendanceDto
            {
                Entries = entries.Select(e => new AttendanceEntryDto { PlayerId = e.Player.Id, Status = e.Status }).ToList()
            };
        }

        private async Task RecordAsync(CurrentUser coach, int eventId, RecordAttendanceDto sheet)
        {
            using var context = _world.NewContext();
            Assert.True(await _world.Attendance(context).RecordAsync(coach, eventId, sheet));
        }

        // ---------- The head coach's switch ----------

        [Fact]
        public async Task Record_WhileTrackingIsSwitchedOff_IsRefusedForEveryone_IncludingTheHeadCoach()
        {
            var headCoach = await _world.AddTeamAsync();   // tracking is off for a new team
            var coordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            var player = await _world.AddPlayerAsync(headCoach, "Walker");
            int eventId = await _world.AddEventAsync(headCoach, TestWorld.Today);
            var sheet = Sheet((player, AttendanceStatus.Present));

            using var context = _world.NewContext();
            var service = _world.Attendance(context);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(headCoach, eventId, sheet));
            Assert.Contains("switched off", error.Message);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(coordinator, eventId, sheet));

            Assert.Equal(0, await context.AttendanceRecords.CountAsync());
        }

        [Fact]
        public async Task Record_WorksOnceTheHeadCoachSwitchesTrackingOn_AndStopsWhenItIsSwitchedOffAgain()
        {
            var headCoach = await _world.AddTeamAsync();
            var positionCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var player = await _world.AddPlayerAsync(headCoach, "Walker");
            int eventId = await _world.AddEventAsync(headCoach, TestWorld.Today);

            using (var context = _world.NewContext())
            {
                // Only the head coach can change the switch
                var settings = new UpdateTeamDto { Name = "Test Tigers", AttendanceTrackingEnabled = true };
                await Assert.ThrowsAsync<ForbiddenException>(() => _world.Teams(context).UpdateAsync(positionCoach, settings));
                Assert.True(await _world.Teams(context).UpdateAsync(headCoach, settings));
            }

            await RecordAsync(positionCoach, eventId, Sheet((player, AttendanceStatus.Present)));

            using (var context = _world.NewContext())
            {
                await _world.Teams(context).UpdateAsync(headCoach, new UpdateTeamDto { Name = "Test Tigers", AttendanceTrackingEnabled = false });
            }

            using var checkContext = _world.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _world.Attendance(checkContext).RecordAsync(positionCoach, eventId, Sheet((player, AttendanceStatus.Late))));

            // What was recorded while tracking was on is kept
            Assert.Equal(AttendanceStatus.Present, (await checkContext.AttendanceRecords.SingleAsync()).Status);
        }

        // ---------- Who may record ----------

        [Fact]
        public async Task Record_ByACoachWhoseAttendancePermissionIsOff_IsForbidden()
        {
            var headCoach = await _world.AddTeamAsync();
            var coach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var player = await _world.AddPlayerAsync(headCoach, "Walker");
            await _world.SetAttendanceTrackingAsync(headCoach, true);
            int eventId = await _world.AddEventAsync(headCoach, TestWorld.Today);

            using (var context = _world.NewContext())
            {
                await _world.Users(context).UpdatePermissionsAsync(headCoach, coach.Id,
                    new UpdatePermissionsDto { CanEditDepthChart = true, CanRecordAttendance = false });
            }
            var withoutPermission = await _world.AsAsync(coach.Id);

            using var recordContext = _world.NewContext();
            var error = await Assert.ThrowsAsync<ForbiddenException>(
                () => _world.Attendance(recordContext).RecordAsync(withoutPermission, eventId, Sheet((player, AttendanceStatus.Present))));
            Assert.Contains("switched off attendance recording for your account", error.Message);
        }

        [Fact]
        public async Task Record_ByAPlayer_IsForbidden_EvenForThemselves()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker");
            await _world.SetAttendanceTrackingAsync(headCoach, true);
            int eventId = await _world.AddEventAsync(headCoach, TestWorld.Today);

            using var context = _world.NewContext();
            var service = _world.Attendance(context);

            await Assert.ThrowsAsync<ForbiddenException>(() => service.RecordAsync(player, eventId, Sheet((player, AttendanceStatus.Present))));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetSheetAsync(player, eventId));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetSummaryAsync(player, null, null));
        }

        // ---------- What can be recorded ----------

        [Fact]
        public async Task Record_ForAnEventThatHasNotHappenedYet_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker");
            await _world.SetAttendanceTrackingAsync(headCoach, true);
            int tomorrowId = await _world.AddEventAsync(headCoach, TestWorld.Today.AddDays(1));
            int todayId = await _world.AddEventAsync(headCoach, TestWorld.Today);

            using var context = _world.NewContext();
            var service = _world.Attendance(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(headCoach, tomorrowId, Sheet((player, AttendanceStatus.Present))));
            Assert.True(await service.RecordAsync(headCoach, todayId, Sheet((player, AttendanceStatus.Present))));
        }

        [Fact]
        public async Task Record_SavesEachPlayersStatus_ThenUpdatesAndClearsThem()
        {
            var headCoach = await _world.AddTeamAsync();
            var coordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            var walker = await _world.AddPlayerAsync(headCoach, "Walker");
            var hughes = await _world.AddPlayerAsync(headCoach, "Hughes");
            var kim = await _world.AddPlayerAsync(headCoach, "Kim");
            await _world.SetAttendanceTrackingAsync(headCoach, true);
            int eventId = await _world.AddEventAsync(headCoach, TestWorld.Today);

            await RecordAsync(headCoach, eventId, Sheet(
                (walker, AttendanceStatus.Present), (hughes, AttendanceStatus.Late), (kim, AttendanceStatus.Absent)));

            // Later a coordinator corrects one status, leaves one as it was, and clears one
            _world.Clock.Advance(TimeSpan.FromHours(1));
            await RecordAsync(coordinator, eventId, Sheet(
                (walker, AttendanceStatus.Present), (hughes, AttendanceStatus.Excused), (kim, null)));

            using var context = _world.NewContext();
            var sheet = await _world.Attendance(context).GetSheetAsync(headCoach, eventId);
            var rows = sheet!.Rows.ToDictionary(r => r.LastName);

            Assert.Equal(AttendanceStatus.Present, rows["Walker"].Status);
            Assert.Equal("Head Coach", rows["Walker"].RecordedBy);          // unchanged, so still the first recorder
            Assert.Equal(AttendanceStatus.Excused, rows["Hughes"].Status);
            Assert.NotEqual("Head Coach", rows["Hughes"].RecordedBy);       // changed by the coordinator
            Assert.Null(rows["Kim"].Status);                                 // cleared
            Assert.Equal(2, sheet.Event.AttendanceCount);
            Assert.Equal(2, await context.AttendanceRecords.CountAsync());   // still one record per player
        }

        [Fact]
        public async Task Record_ForSomeoneWhoIsNotAPlayerOfThisTeam_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync("Test Tigers");
            var coach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach);
            var player = await _world.AddPlayerAsync(headCoach, "Walker");
            var otherTeamCoach = await _world.AddTeamAsync("Test Bears");
            var otherTeamPlayer = await _world.AddPlayerAsync(otherTeamCoach, "Stone");
            await _world.SetAttendanceTrackingAsync(headCoach, true);
            int eventId = await _world.AddEventAsync(headCoach, TestWorld.Today);

            using var context = _world.NewContext();
            var service = _world.Attendance(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(headCoach, eventId, Sheet((otherTeamPlayer, AttendanceStatus.Present))));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(headCoach, eventId, Sheet((coach, AttendanceStatus.Present))));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(headCoach, eventId,
                Sheet((player, AttendanceStatus.Present), (player, AttendanceStatus.Late))));   // the same player twice

            Assert.Equal(0, await context.AttendanceRecords.CountAsync());
        }

        // ---------- Reading ----------

        [Fact]
        public async Task Sheet_ListsActivePlayers_AndSaysWhetherAndWhyRecordingIsAllowed()
        {
            var headCoach = await _world.AddTeamAsync();
            var active = await _world.AddPlayerAsync(headCoach, "Active");
            var deactivated = await _world.AddPlayerAsync(headCoach, "Deactivated");
            using (var context = _world.NewContext())
            {
                (await context.Users.SingleAsync(u => u.Id == deactivated.Id)).IsActive = false;
                await context.SaveChangesAsync();
            }
            int todayId = await _world.AddEventAsync(headCoach, TestWorld.Today);
            int tomorrowId = await _world.AddEventAsync(headCoach, TestWorld.Today.AddDays(1));

            using (var context = _world.NewContext())
            {
                var whileOff = await _world.Attendance(context).GetSheetAsync(headCoach, todayId);
                Assert.Equal(new[] { "Active" }, whileOff!.Rows.Select(r => r.LastName));
                Assert.False(whileOff.TrackingEnabled);
                Assert.False(whileOff.CanRecord);
                Assert.Contains("switched off", whileOff.CannotRecordReason);
            }

            await _world.SetAttendanceTrackingAsync(headCoach, true);

            using var onContext = _world.NewContext();
            var service = _world.Attendance(onContext);

            var today = await service.GetSheetAsync(headCoach, todayId);
            Assert.True(today!.CanRecord);
            Assert.Null(today.CannotRecordReason);

            var tomorrow = await service.GetSheetAsync(headCoach, tomorrowId);
            Assert.False(tomorrow!.CanRecord);
            Assert.Contains("day of the event", tomorrow.CannotRecordReason);
        }

        [Fact]
        public async Task MyAttendance_ShowsAPlayerOnlyTheirOwnRecords_NewestFirst()
        {
            var headCoach = await _world.AddTeamAsync();
            var walker = await _world.AddPlayerAsync(headCoach, "Walker");
            var hughes = await _world.AddPlayerAsync(headCoach, "Hughes");
            await _world.SetAttendanceTrackingAsync(headCoach, true);
            int earlierId = await _world.AddEventAsync(headCoach, TestWorld.Today.AddDays(-2), EventType.Practice, "Earlier practice");
            int laterId = await _world.AddEventAsync(headCoach, TestWorld.Today, EventType.Meeting, "Meeting today");

            await RecordAsync(headCoach, earlierId, Sheet((walker, AttendanceStatus.Late), (hughes, AttendanceStatus.Absent)));
            await RecordAsync(headCoach, laterId, Sheet((walker, AttendanceStatus.Present)));

            using var context = _world.NewContext();
            var service = _world.Attendance(context);

            var walkers = await service.GetMyAttendanceAsync(walker);
            Assert.Equal(new[] { "Meeting today", "Earlier practice" }, walkers.Select(r => r.Title));
            Assert.Equal(new[] { AttendanceStatus.Present, AttendanceStatus.Late }, walkers.Select(r => r.Status));

            var hughess = await service.GetMyAttendanceAsync(hughes);
            Assert.Equal(AttendanceStatus.Absent, Assert.Single(hughess).Status);
        }

        [Fact]
        public async Task Summary_CountsEachStatusPerPlayer_WithinTheChosenDates()
        {
            var headCoach = await _world.AddTeamAsync();
            var walker = await _world.AddPlayerAsync(headCoach, "Walker");
            var hughes = await _world.AddPlayerAsync(headCoach, "Hughes");
            await _world.SetAttendanceTrackingAsync(headCoach, true);
            int lastWeekId = await _world.AddEventAsync(headCoach, TestWorld.Today.AddDays(-7));
            int yesterdayId = await _world.AddEventAsync(headCoach, TestWorld.Today.AddDays(-1));
            int todayId = await _world.AddEventAsync(headCoach, TestWorld.Today);

            await RecordAsync(headCoach, lastWeekId, Sheet((walker, AttendanceStatus.Absent), (hughes, AttendanceStatus.Present)));
            await RecordAsync(headCoach, yesterdayId, Sheet((walker, AttendanceStatus.Present), (hughes, AttendanceStatus.Late)));
            await RecordAsync(headCoach, todayId, Sheet((walker, AttendanceStatus.Present)));

            using var context = _world.NewContext();
            var service = _world.Attendance(context);

            var whole = (await service.GetSummaryAsync(headCoach, null, null)).ToDictionary(s => s.LastName);
            Assert.Equal((2, 0, 1, 0), (whole["Walker"].Present, whole["Walker"].Late, whole["Walker"].Absent, whole["Walker"].Excused));
            Assert.Equal((1, 1, 0, 0), (whole["Hughes"].Present, whole["Hughes"].Late, whole["Hughes"].Absent, whole["Hughes"].Excused));

            // From yesterday on, last week's absence is left out
            var recent = (await service.GetSummaryAsync(headCoach, TestWorld.Today.AddDays(-1), null)).ToDictionary(s => s.LastName);
            Assert.Equal(0, recent["Walker"].Absent);
            Assert.Equal(2, recent["Walker"].Present);
        }

        [Fact]
        public async Task AnotherTeamsEventsAndRecords_CannotBeReached()
        {
            var tigersCoach = await _world.AddTeamAsync("Test Tigers");
            var tigersPlayer = await _world.AddPlayerAsync(tigersCoach, "Walker");
            await _world.SetAttendanceTrackingAsync(tigersCoach, true);
            int tigersEventId = await _world.AddEventAsync(tigersCoach, TestWorld.Today);
            await RecordAsync(tigersCoach, tigersEventId, Sheet((tigersPlayer, AttendanceStatus.Present)));

            var bearsCoach = await _world.AddTeamAsync("Test Bears");
            var bearsPlayer = await _world.AddPlayerAsync(bearsCoach, "Stone");
            await _world.SetAttendanceTrackingAsync(bearsCoach, true);

            using var context = _world.NewContext();
            var service = _world.Attendance(context);

            Assert.Null(await service.GetSheetAsync(bearsCoach, tigersEventId));
            Assert.False(await service.RecordAsync(bearsCoach, tigersEventId, Sheet((bearsPlayer, AttendanceStatus.Absent))));
            Assert.Empty(await service.GetMyAttendanceAsync(bearsPlayer));

            var bearsSummary = await service.GetSummaryAsync(bearsCoach, null, null);
            Assert.Equal("Stone", Assert.Single(bearsSummary).LastName);
            Assert.Equal(0, bearsSummary[0].Present);
        }
    }
}
