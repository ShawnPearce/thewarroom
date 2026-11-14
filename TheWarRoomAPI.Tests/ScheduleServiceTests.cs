using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The team schedule: who may change it, what a valid event is, and keeping teams apart
    public class ScheduleServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        private static SaveScheduledEventDto NewEvent(string title = "Practice", EventType type = EventType.Practice)
        {
            return new SaveScheduledEventDto
            {
                EventType = type,
                Title = title,
                EventDate = TestWorld.Today.AddDays(1),
                StartTime = new TimeOnly(15, 30),
                EndTime = new TimeOnly(17, 30),
                Location = "Practice field"
            };
        }

        [Fact]
        public async Task Create_IsForTheHeadCoachAndCoordinators()
        {
            var headCoach = await _world.AddTeamAsync();
            var coordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Defence);
            var positionCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var player = await _world.AddPlayerAsync(headCoach, "Walker");

            using var context = _world.NewContext();
            var service = _world.Schedule(context);

            var byHeadCoach = await service.CreateAsync(headCoach, NewEvent("Walkthrough"));
            var byCoordinator = await service.CreateAsync(coordinator, NewEvent("Defence meeting", EventType.Meeting));
            Assert.True(byHeadCoach.CanEdit);
            Assert.Equal(EventType.Meeting, byCoordinator.EventType);

            await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(positionCoach, NewEvent()));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(player, NewEvent()));

            // Everyone on the team can still read the schedule
            var seenByPlayer = await service.GetEventsAsync(player, null, null, null);
            Assert.Equal(2, seenByPlayer.Count);
            Assert.All(seenByPlayer, e => Assert.False(e.CanEdit));
        }

        [Theory]
        [InlineData(15, 30)]   // ends when it starts
        [InlineData(14, 0)]    // ends before it starts
        public async Task Create_EndTimeNotAfterTheStart_IsRejected(int endHour, int endMinute)
        {
            var headCoach = await _world.AddTeamAsync();
            var dto = NewEvent();
            dto.EndTime = new TimeOnly(endHour, endMinute);

            using var context = _world.NewContext();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _world.Schedule(context).CreateAsync(headCoach, dto));

            Assert.Contains("end time", error.Message);
        }

        [Fact]
        public async Task Create_WithoutAnEndTime_IsAllowed_AndBlankNotesAreStoredAsNone()
        {
            var headCoach = await _world.AddTeamAsync();
            var dto = NewEvent("  vs Central  ", EventType.Game);
            dto.EndTime = null;
            dto.Notes = "   ";

            using var context = _world.NewContext();
            var created = await _world.Schedule(context).CreateAsync(headCoach, dto);

            Assert.Equal("vs Central", created.Title);
            Assert.Null(created.EndTime);
            Assert.Null(created.Notes);
        }

        [Fact]
        public async Task Events_ComeBackInDateThenTimeOrder_AndCanBeFiltered()
        {
            var headCoach = await _world.AddTeamAsync();
            await _world.AddEventAsync(headCoach, TestWorld.Today.AddDays(3), EventType.Game, "Game");
            await _world.AddEventAsync(headCoach, TestWorld.Today.AddDays(-2), EventType.Practice, "Earlier practice");
            await _world.AddEventAsync(headCoach, TestWorld.Today, EventType.Meeting, "Meeting today");
            await _world.AddEventAsync(headCoach, TestWorld.Today.AddDays(1), EventType.Practice, "Later practice");

            using var context = _world.NewContext();
            var service = _world.Schedule(context);

            var all = await service.GetEventsAsync(headCoach, null, null, null);
            Assert.Equal(new[] { "Earlier practice", "Meeting today", "Later practice", "Game" }, all.Select(e => e.Title));

            var upcoming = await service.GetEventsAsync(headCoach, TestWorld.Today, null, null);
            Assert.Equal(new[] { "Meeting today", "Later practice", "Game" }, upcoming.Select(e => e.Title));

            var past = await service.GetEventsAsync(headCoach, null, TestWorld.Today.AddDays(-1), null);
            Assert.Equal(new[] { "Earlier practice" }, past.Select(e => e.Title));

            var practices = await service.GetEventsAsync(headCoach, null, null, EventType.Practice);
            Assert.Equal(new[] { "Earlier practice", "Later practice" }, practices.Select(e => e.Title));
        }

        [Fact]
        public async Task Update_ChangesTheEvent_AndIsRefusedForAPositionCoach()
        {
            var headCoach = await _world.AddTeamAsync();
            var positionCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach);
            int eventId = await _world.AddEventAsync(headCoach, TestWorld.Today.AddDays(1));
            var changes = NewEvent("Moved indoors");
            changes.Location = "Gym";

            using var context = _world.NewContext();
            var service = _world.Schedule(context);

            await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(positionCoach, eventId, changes));
            Assert.True(await service.UpdateAsync(headCoach, eventId, changes));

            var saved = await service.GetEventAsync(headCoach, eventId);
            Assert.Equal("Moved indoors", saved!.Title);
            Assert.Equal("Gym", saved.Location);
        }

        [Fact]
        public async Task Delete_OnceAttendanceIsRecorded_IsForTheHeadCoachOnly_AndTakesTheRecordsWithIt()
        {
            var headCoach = await _world.AddTeamAsync();
            var coordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            var player = await _world.AddPlayerAsync(headCoach, "Walker");
            await _world.SetAttendanceTrackingAsync(headCoach, true);
            int plainEventId = await _world.AddEventAsync(headCoach, TestWorld.Today.AddDays(2));
            int recordedEventId = await _world.AddEventAsync(headCoach, TestWorld.Today);

            using (var context = _world.NewContext())
            {
                await _world.Attendance(context).RecordAsync(headCoach, recordedEventId, new RecordAttendanceDto
                {
                    Entries = { new AttendanceEntryDto { PlayerId = player.Id, Status = AttendanceStatus.Present } }
                });
            }

            using (var context = _world.NewContext())
            {
                var service = _world.Schedule(context);

                // A coordinator can delete an event that has no attendance...
                Assert.True(await service.DeleteAsync(coordinator, plainEventId));

                // ...but not one whose attendance would be lost
                await Assert.ThrowsAsync<ForbiddenException>(() => service.DeleteAsync(coordinator, recordedEventId));
                Assert.True(await service.DeleteAsync(headCoach, recordedEventId));
            }

            using var checkContext = _world.NewContext();
            Assert.Equal(0, await checkContext.ScheduledEvents.CountAsync());
            Assert.Equal(0, await checkContext.AttendanceRecords.CountAsync());
        }

        [Fact]
        public async Task AnotherTeamsEvents_CannotBeReached()
        {
            var tigersCoach = await _world.AddTeamAsync("Test Tigers");
            int tigersEventId = await _world.AddEventAsync(tigersCoach, TestWorld.Today);
            var bearsCoach = await _world.AddTeamAsync("Test Bears");

            using var context = _world.NewContext();
            var service = _world.Schedule(context);

            Assert.Empty(await service.GetEventsAsync(bearsCoach, null, null, null));
            Assert.Null(await service.GetEventAsync(bearsCoach, tigersEventId));
            Assert.False(await service.UpdateAsync(bearsCoach, tigersEventId, NewEvent("Taken over")));
            Assert.False(await service.DeleteAsync(bearsCoach, tigersEventId));

            Assert.Equal("Practice", (await service.GetEventAsync(tigersCoach, tigersEventId))!.Title);
        }
    }
}
