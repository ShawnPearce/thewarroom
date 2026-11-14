using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Weekly depth charts: ordering rules, who may edit, and keeping teams apart
    public class DepthChartServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        // Adds a player to a position as the given user and returns the ranks at that position afterwards
        private async Task<DepthChartSlotDto> AddAsync(CurrentUser actingUser, int weekId, string positionCode, CurrentUser player)
        {
            using var context = _world.NewContext();
            int positionId = await _world.PositionIdAsync(actingUser, positionCode);
            var slot = await _world.DepthChart(context).AddPlayerAsync(actingUser, weekId, positionId, new AddPlacementDto { PlayerId = player.Id });
            return slot!;
        }

        private async Task<DepthChartSlotDto> SlotAsync(CurrentUser user, int weekId, string positionCode)
        {
            using var context = _world.NewContext();
            int positionId = await _world.PositionIdAsync(user, positionCode);
            return (await _world.DepthChart(context).GetSlotAsync(user, weekId, positionId))!;
        }

        // ---------- Ordering ----------

        [Fact]
        public async Task Add_PutsEachNewPlayerAtTheBottom()
        {
            var headCoach = await _world.AddTeamAsync();
            var first = await _world.AddPlayerAsync(headCoach, "First", "Receivers");
            var second = await _world.AddPlayerAsync(headCoach, "Second", "Receivers");
            var third = await _world.AddPlayerAsync(headCoach, "Third", "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);

            await AddAsync(headCoach, weekId, "X", first);
            await AddAsync(headCoach, weekId, "X", second);
            var slot = await AddAsync(headCoach, weekId, "X", third);

            Assert.Equal(new[] { "First", "Second", "Third" }, slot.Players.Select(p => p.LastName));
            Assert.Equal(new[] { 1, 2, 3 }, slot.Players.Select(p => p.Rank));
        }

        [Fact]
        public async Task SamePlayer_CanHoldDifferentRanksAtDifferentPositions()
        {
            var headCoach = await _world.AddTeamAsync();
            var walker = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var hughes = await _world.AddPlayerAsync(headCoach, "Hughes", "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);

            // Walker starts at X and backs up Hughes at Y
            await AddAsync(headCoach, weekId, "X", walker);
            await AddAsync(headCoach, weekId, "Y", hughes);
            await AddAsync(headCoach, weekId, "Y", walker);

            using var context = _world.NewContext();
            var mine = await _world.DepthChart(context).GetMyPlacementsAsync(walker, weekId);

            Assert.NotNull(mine);
            Assert.Equal(2, mine!.Count);
            Assert.Equal(1, mine.Single(p => p.Code == "X").Rank);
            Assert.Equal(2, mine.Single(p => p.Code == "Y").Rank);
            Assert.Equal(2, mine.Single(p => p.Code == "Y").PlayersAtPosition);
        }

        [Fact]
        public async Task Add_PlayerAlreadyAtThatPosition_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);
            await AddAsync(headCoach, weekId, "X", player);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AddAsync(headCoach, weekId, "X", player));

            Assert.Contains("already listed", error.Message);
        }

        [Fact]
        public async Task Add_PlayerWhoIsNotInThePositionsGroup_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync();
            var lineman = await _world.AddPlayerAsync(headCoach, "Kowalski", "Offensive Line");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => AddAsync(headCoach, weekId, "X", lineman));

            Assert.Contains("not in the Receivers group", error.Message);
        }

        [Fact]
        public async Task Add_DeactivatedPlayer_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);
            using (var context = _world.NewContext())
            {
                (await context.Users.SingleAsync(u => u.Id == player.Id)).IsActive = false;
                await context.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() => AddAsync(headCoach, weekId, "X", player));
        }

        [Fact]
        public async Task Add_PlayerMarkedInactiveOnTheRoster_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);
            using (var context = _world.NewContext())
            {
                (await context.RosterEntries.SingleAsync(r => r.UserId == player.Id)).Status = RosterStatus.Inactive;
                await context.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() => AddAsync(headCoach, weekId, "X", player));
        }

        [Fact]
        public async Task Move_SwapsThePlayerWithTheNeighbour()
        {
            var headCoach = await _world.AddTeamAsync();
            var first = await _world.AddPlayerAsync(headCoach, "First", "Receivers");
            var second = await _world.AddPlayerAsync(headCoach, "Second", "Receivers");
            var third = await _world.AddPlayerAsync(headCoach, "Third", "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);
            await AddAsync(headCoach, weekId, "X", first);
            await AddAsync(headCoach, weekId, "X", second);
            var slot = await AddAsync(headCoach, weekId, "X", third);
            int thirdPlacementId = slot.Players.Single(p => p.LastName == "Third").PlacementId;

            using (var context = _world.NewContext())
            {
                Assert.True(await _world.DepthChart(context).MovePlacementAsync(headCoach, thirdPlacementId, MoveDirection.Up));
            }
            var afterUp = await SlotAsync(headCoach, weekId, "X");
            Assert.Equal(new[] { "First", "Third", "Second" }, afterUp.Players.Select(p => p.LastName));
            Assert.Equal(new[] { 1, 2, 3 }, afterUp.Players.Select(p => p.Rank));

            using (var context = _world.NewContext())
            {
                Assert.True(await _world.DepthChart(context).MovePlacementAsync(headCoach, thirdPlacementId, MoveDirection.Down));
            }
            var afterDown = await SlotAsync(headCoach, weekId, "X");
            Assert.Equal(new[] { "First", "Second", "Third" }, afterDown.Players.Select(p => p.LastName));
        }

        [Fact]
        public async Task Move_StarterUp_ChangesNothing()
        {
            var headCoach = await _world.AddTeamAsync();
            var first = await _world.AddPlayerAsync(headCoach, "First", "Receivers");
            var second = await _world.AddPlayerAsync(headCoach, "Second", "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);
            var slot = await AddAsync(headCoach, weekId, "X", first);
            await AddAsync(headCoach, weekId, "X", second);

            using (var context = _world.NewContext())
            {
                await _world.DepthChart(context).MovePlacementAsync(headCoach, slot.Players[0].PlacementId, MoveDirection.Up);
            }

            var after = await SlotAsync(headCoach, weekId, "X");
            Assert.Equal(new[] { "First", "Second" }, after.Players.Select(p => p.LastName));
        }

        [Fact]
        public async Task Remove_MovesThePlayersBelowUp()
        {
            var headCoach = await _world.AddTeamAsync();
            var first = await _world.AddPlayerAsync(headCoach, "First", "Receivers");
            var second = await _world.AddPlayerAsync(headCoach, "Second", "Receivers");
            var third = await _world.AddPlayerAsync(headCoach, "Third", "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);
            var slot = await AddAsync(headCoach, weekId, "X", first);
            await AddAsync(headCoach, weekId, "X", second);
            await AddAsync(headCoach, weekId, "X", third);

            using (var context = _world.NewContext())
            {
                Assert.True(await _world.DepthChart(context).RemovePlacementAsync(headCoach, slot.Players[0].PlacementId));
            }

            var after = await SlotAsync(headCoach, weekId, "X");
            Assert.Equal(new[] { "Second", "Third" }, after.Players.Select(p => p.LastName));
            Assert.Equal(new[] { 1, 2 }, after.Players.Select(p => p.Rank));
        }

        [Fact]
        public async Task Slot_OffersOnlyGroupMembersWhoAreNotListedYet()
        {
            var headCoach = await _world.AddTeamAsync();
            var listed = await _world.AddPlayerAsync(headCoach, "Listed", "Receivers");
            await _world.AddPlayerAsync(headCoach, "Available", "Receivers");
            await _world.AddPlayerAsync(headCoach, "Lineman", "Offensive Line");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);

            var slot = await AddAsync(headCoach, weekId, "X", listed);

            Assert.Equal(new[] { "Available" }, slot.EligiblePlayers.Select(p => p.LastName));
        }

        // ---------- Who may edit ----------

        [Fact]
        public async Task PositionCoach_EditsOwnGroup_ButNotAnotherGroup()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var receiver = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var lineman = await _world.AddPlayerAsync(headCoach, "Kowalski", "Offensive Line");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);

            var slot = await AddAsync(receiversCoach, weekId, "X", receiver);
            Assert.Single(slot.Players);

            // The offensive line is in the same unit, but it is not this coach's group
            await Assert.ThrowsAsync<ForbiddenException>(() => AddAsync(receiversCoach, weekId, "LT", lineman));
        }

        [Fact]
        public async Task Coordinator_EditsOwnUnit_ButNotAnotherUnit()
        {
            var headCoach = await _world.AddTeamAsync();
            var offenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            var receiver = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var lineman = await _world.AddPlayerAsync(headCoach, "Kowalski", "Offensive Line");
            var linebacker = await _world.AddPlayerAsync(headCoach, "Ford", "Linebackers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);

            // Any offensive group is fine without being assigned to it
            await AddAsync(offenceCoordinator, weekId, "X", receiver);
            await AddAsync(offenceCoordinator, weekId, "LT", lineman);

            await Assert.ThrowsAsync<ForbiddenException>(() => AddAsync(offenceCoordinator, weekId, "MLB", linebacker));
        }

        [Fact]
        public async Task Coach_WithDepthChartEditingSwitchedOff_IsRefused_UntilItIsSwitchedBackOn()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            var receiver = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);

            using (var context = _world.NewContext())
            {
                await _world.Users(context).UpdatePermissionsAsync(headCoach, receiversCoach.Id,
                    new UpdatePermissionsDto { CanEditDepthChart = false, CanRecordAttendance = true });
            }
            var switchedOff = await _world.AsAsync(receiversCoach.Id);
            var error = await Assert.ThrowsAsync<ForbiddenException>(() => AddAsync(switchedOff, weekId, "X", receiver));
            Assert.Contains("switched off", error.Message);

            using (var context = _world.NewContext())
            {
                await _world.Users(context).UpdatePermissionsAsync(headCoach, receiversCoach.Id,
                    new UpdatePermissionsDto { CanEditDepthChart = true, CanRecordAttendance = true });
            }
            var switchedOn = await _world.AsAsync(receiversCoach.Id);
            var slot = await AddAsync(switchedOn, weekId, "X", receiver);
            Assert.Single(slot.Players);
        }

        [Fact]
        public async Task Player_CanReadTheChart_ButCannotChangeIt()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);
            var slot = await AddAsync(headCoach, weekId, "X", player);

            using var context = _world.NewContext();
            var service = _world.DepthChart(context);

            var chart = await service.GetWeekAsync(player, weekId);
            Assert.NotNull(chart);
            Assert.All(chart!.Groups, g => Assert.False(g.CanEdit));

            await Assert.ThrowsAsync<ForbiddenException>(
                () => service.RemovePlacementAsync(player, slot.Players[0].PlacementId));
            await Assert.ThrowsAsync<ForbiddenException>(
                () => service.MovePlacementAsync(player, slot.Players[0].PlacementId, MoveDirection.Down));
        }

        // ---------- Weeks ----------

        [Fact]
        public async Task CreateWeek_StoresTheMondayOfTheChosenDay()
        {
            var headCoach = await _world.AddTeamAsync();

            using var context = _world.NewContext();
            var week = await _world.DepthChart(context).CreateWeekAsync(headCoach, new CreateDepthChartWeekDto
            {
                WeekStartDate = new DateOnly(2026, 9, 10),   // a Thursday
                Label = "  vs Central  "
            });

            Assert.Equal(new DateOnly(2026, 9, 7), week.WeekStartDate);
            Assert.Equal("vs Central", week.Label);
        }

        [Fact]
        public async Task CreateWeek_SecondChartForTheSameWeek_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync();
            await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);

            using var context = _world.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _world.DepthChart(context).CreateWeekAsync(headCoach,
                new CreateDepthChartWeekDto { WeekStartDate = TestWorld.ThisMonday.AddDays(3) }));
        }

        [Fact]
        public async Task CreateWeek_IsForTheHeadCoachAndCoordinators()
        {
            var headCoach = await _world.AddTeamAsync();
            var coordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Defence);
            var positionCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");

            using var context = _world.NewContext();
            var service = _world.DepthChart(context);

            await service.CreateWeekAsync(coordinator, new CreateDepthChartWeekDto { WeekStartDate = TestWorld.ThisMonday });
            await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateWeekAsync(positionCoach,
                new CreateDepthChartWeekDto { WeekStartDate = TestWorld.ThisMonday.AddDays(7) }));
        }

        [Fact]
        public async Task CreateWeek_AsACopy_CarriesTheOrderOver_ExceptPlayersWhoLeft()
        {
            var headCoach = await _world.AddTeamAsync();
            var first = await _world.AddPlayerAsync(headCoach, "First", "Receivers");
            var leaving = await _world.AddPlayerAsync(headCoach, "Leaving", "Receivers");
            var third = await _world.AddPlayerAsync(headCoach, "Third", "Receivers");
            int lastWeekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday.AddDays(-7));
            await AddAsync(headCoach, lastWeekId, "X", first);
            await AddAsync(headCoach, lastWeekId, "X", leaving);
            await AddAsync(headCoach, lastWeekId, "X", third);

            using (var context = _world.NewContext())
            {
                (await context.Users.SingleAsync(u => u.Id == leaving.Id)).IsActive = false;
                await context.SaveChangesAsync();
            }

            DepthChartWeekDto newWeek;
            using (var context = _world.NewContext())
            {
                newWeek = await _world.DepthChart(context).CreateWeekAsync(headCoach, new CreateDepthChartWeekDto
                {
                    WeekStartDate = TestWorld.ThisMonday,
                    CopyFromWeekId = lastWeekId
                });
            }

            var copied = await SlotAsync(headCoach, newWeek.Id, "X");
            Assert.Equal(new[] { "First", "Third" }, copied.Players.Select(p => p.LastName));
            Assert.Equal(new[] { 1, 2 }, copied.Players.Select(p => p.Rank));

            // The earlier week keeps its history
            var original = await SlotAsync(headCoach, lastWeekId, "X");
            Assert.Equal(3, original.Players.Count);
        }

        [Fact]
        public async Task DeleteWeek_IsForTheHeadCoach_AndRemovesItsPlacements()
        {
            var headCoach = await _world.AddTeamAsync();
            var coordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);
            await AddAsync(headCoach, weekId, "X", player);

            using (var context = _world.NewContext())
            {
                await Assert.ThrowsAsync<ForbiddenException>(() => _world.DepthChart(context).DeleteWeekAsync(coordinator, weekId));
            }

            using (var context = _world.NewContext())
            {
                Assert.True(await _world.DepthChart(context).DeleteWeekAsync(headCoach, weekId));
            }

            using var checkContext = _world.NewContext();
            Assert.Equal(0, await checkContext.DepthChartWeeks.CountAsync());
            Assert.Equal(0, await checkContext.DepthChartPlacements.CountAsync());
        }

        [Fact]
        public async Task CurrentWeek_IsThisWeek_OrTheMostRecentEarlierOne()
        {
            var headCoach = await _world.AddTeamAsync();
            int lastWeekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday.AddDays(-7));
            await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday.AddDays(7));   // next week must not be picked

            using (var context = _world.NewContext())
            {
                var current = await _world.DepthChart(context).GetCurrentWeekAsync(headCoach);
                Assert.Equal(lastWeekId, current!.Week.Id);
            }

            int thisWeekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);
            using (var context = _world.NewContext())
            {
                var current = await _world.DepthChart(context).GetCurrentWeekAsync(headCoach);
                Assert.Equal(thisWeekId, current!.Week.Id);
            }
        }

        // ---------- Keeping teams apart ----------

        [Fact]
        public async Task AnotherTeamsWeekPositionAndPlacement_CannotBeReached()
        {
            var tigersCoach = await _world.AddTeamAsync("Test Tigers");
            var tigersPlayer = await _world.AddPlayerAsync(tigersCoach, "Walker", "Receivers");
            int tigersWeekId = await _world.AddWeekAsync(tigersCoach, TestWorld.ThisMonday);
            var tigersSlot = await AddAsync(tigersCoach, tigersWeekId, "X", tigersPlayer);
            int tigersPositionId = await _world.PositionIdAsync(tigersCoach, "X");

            var bearsCoach = await _world.AddTeamAsync("Test Bears");
            var bearsPlayer = await _world.AddPlayerAsync(bearsCoach, "Stone", "Receivers");
            int bearsWeekId = await _world.AddWeekAsync(bearsCoach, TestWorld.ThisMonday);
            int bearsPositionId = await _world.PositionIdAsync(bearsCoach, "X");

            using var context = _world.NewContext();
            var service = _world.DepthChart(context);

            // Even a head coach gets "not found" for another team's records
            Assert.Null(await service.GetWeekAsync(bearsCoach, tigersWeekId));
            Assert.Null(await service.GetSlotAsync(bearsCoach, tigersWeekId, bearsPositionId));
            Assert.Null(await service.GetSlotAsync(bearsCoach, bearsWeekId, tigersPositionId));
            Assert.Null(await service.GetMyPlacementsAsync(bearsCoach, tigersWeekId));
            Assert.Null(await service.AddPlayerAsync(bearsCoach, tigersWeekId, tigersPositionId, new AddPlacementDto { PlayerId = bearsPlayer.Id }));
            Assert.False(await service.MovePlacementAsync(bearsCoach, tigersSlot.Players[0].PlacementId, MoveDirection.Down));
            Assert.False(await service.RemovePlacementAsync(bearsCoach, tigersSlot.Players[0].PlacementId));
            Assert.False(await service.DeleteWeekAsync(bearsCoach, tigersWeekId));

            // A player of another team cannot be placed on this team's chart
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddPlayerAsync(bearsCoach, bearsWeekId, bearsPositionId,
                new AddPlacementDto { PlayerId = tigersPlayer.Id }));

            // And a week cannot be started as a copy of another team's week
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateWeekAsync(bearsCoach,
                new CreateDepthChartWeekDto { WeekStartDate = TestWorld.ThisMonday.AddDays(7), CopyFromWeekId = tigersWeekId }));
        }
    }
}
