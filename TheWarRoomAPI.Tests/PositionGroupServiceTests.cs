using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Position groups and positions, and who belongs to each group
    public class PositionGroupServiceTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        [Fact]
        public async Task RegisteringATeam_GivesItTheStandardGroupsAndPositions()
        {
            LoginResultDto registered;
            using (var context = _world.NewContext())
            {
                var teams = _world.Teams(context);
                registered = await teams.RegisterAsync(new RegisterTeamDto
                {
                    TeamName = "New Team", FirstName = "Casey", LastName = "Coach",
                    Email = "casey@example.com", Password = "Correct-Horse-1"
                });
            }
            var headCoach = await _world.AsAsync(registered.UserId);

            using var checkContext = _world.NewContext();
            var groups = await _world.Groups(checkContext).GetGroupsAsync(headCoach);

            Assert.Equal(9, groups.Count);
            Assert.Equal(29, groups.Sum(g => g.Positions.Count));
            Assert.Equal(new[] { "X", "Y", "Z" }, groups.Single(g => g.Name == "Receivers").Positions.Select(p => p.Code));
            Assert.Contains(groups, g => g.Unit == TeamUnit.Defence);
            Assert.Contains(groups, g => g.Unit == TeamUnit.SpecialTeams);
        }

        [Fact]
        public async Task CreateGroup_SameNameInTheSameUnit_IsRejected_ButAnotherUnitMayUseIt()
        {
            var headCoach = await _world.AddTeamAsync();

            using var context = _world.NewContext();
            var service = _world.Groups(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateGroupAsync(headCoach,
                new SavePositionGroupDto { Unit = TeamUnit.Offence, Name = "receivers" }));   // capital letters do not matter

            var created = await service.CreateGroupAsync(headCoach, new SavePositionGroupDto { Unit = TeamUnit.SpecialTeams, Name = "Receivers" });
            Assert.Equal(TeamUnit.SpecialTeams, created.Unit);
        }

        [Fact]
        public async Task GroupsAndPositions_AreManagedByTheHeadCoachOrTheUnitsCoordinator()
        {
            var headCoach = await _world.AddTeamAsync();
            var offenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");
            int receivers = await _world.GroupIdAsync(headCoach, "Receivers");
            int linebackers = await _world.GroupIdAsync(headCoach, "Linebackers");

            using var context = _world.NewContext();
            var service = _world.Groups(context);

            // The offence coordinator can add a position on offence...
            var slot = await service.CreatePositionAsync(offenceCoordinator,
                new SavePositionDto { PositionGroupId = receivers, Code = "slot", Name = "Slot Receiver" });
            Assert.Equal("SLOT", slot.Code);   // codes are stored in capitals
            Assert.Equal(receivers, slot.PositionGroupId);

            // ...but not on defence, and a position coach cannot at all
            await Assert.ThrowsAsync<ForbiddenException>(() => service.CreatePositionAsync(offenceCoordinator,
                new SavePositionDto { PositionGroupId = linebackers, Code = "ROV", Name = "Rover" }));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.CreatePositionAsync(receiversCoach,
                new SavePositionDto { PositionGroupId = receivers, Code = "F", Name = "Flex" }));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.RenameGroupAsync(receiversCoach, receivers,
                new SavePositionGroupDto { Name = "Wideouts" }));
        }

        [Fact]
        public async Task PositionCode_IsUsedOncePerTeam()
        {
            var headCoach = await _world.AddTeamAsync();
            int tightEnds = await _world.GroupIdAsync(headCoach, "Tight Ends");

            using var context = _world.NewContext();
            // "X" already exists in Receivers; a different group still cannot reuse it
            await Assert.ThrowsAsync<InvalidOperationException>(() => _world.Groups(context).CreatePositionAsync(headCoach,
                new SavePositionDto { PositionGroupId = tightEnds, Code = "x", Name = "Extra" }));
        }

        [Fact]
        public async Task DeleteGroup_WithPositions_IsRejected_AndAnEmptyGroupTakesItsAssignmentsWithIt()
        {
            var headCoach = await _world.AddTeamAsync();
            int receivers = await _world.GroupIdAsync(headCoach, "Receivers");

            using var context = _world.NewContext();
            var service = _world.Groups(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteGroupAsync(headCoach, receivers));

            var scoutTeam = await service.CreateGroupAsync(headCoach, new SavePositionGroupDto { Unit = TeamUnit.Offence, Name = "Scout Team" });
            var coach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach);
            var player = await _world.AddPlayerAsync(headCoach, "Walker");
            await service.SetCoachesAsync(headCoach, scoutTeam.Id, new SetGroupMembersDto { UserIds = new List<int> { coach.Id } });
            await service.SetPlayersAsync(headCoach, scoutTeam.Id, new SetGroupMembersDto { UserIds = new List<int> { player.Id } });

            Assert.True(await service.DeleteGroupAsync(headCoach, scoutTeam.Id));

            using var checkContext = _world.NewContext();
            Assert.False(await checkContext.CoachGroupAssignments.AnyAsync(a => a.PositionGroupId == scoutTeam.Id));
            Assert.False(await checkContext.PlayerGroupAssignments.AnyAsync(a => a.PositionGroupId == scoutTeam.Id));
            Assert.True(await checkContext.Users.AnyAsync(u => u.Id == player.Id));   // the people themselves stay
        }

        [Fact]
        public async Task DeletePosition_ThatIsOnADepthChart_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);
            int x = await _world.PositionIdAsync(headCoach, "X");
            int z = await _world.PositionIdAsync(headCoach, "Z");

            using var context = _world.NewContext();
            await _world.DepthChart(context).AddPlayerAsync(headCoach, weekId, x, new AddPlacementDto { PlayerId = player.Id });
            var service = _world.Groups(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeletePositionAsync(headCoach, x));
            Assert.True(await service.DeletePositionAsync(headCoach, z));   // nobody is listed at Z
        }

        [Fact]
        public async Task SetCoaches_IsForTheHeadCoach_AndTakesOnlyThisTeamsCoaches()
        {
            var headCoach = await _world.AddTeamAsync();
            var offenceCoordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            var coach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach);
            var player = await _world.AddPlayerAsync(headCoach, "Walker");
            int receivers = await _world.GroupIdAsync(headCoach, "Receivers");

            using var context = _world.NewContext();
            var service = _world.Groups(context);

            await Assert.ThrowsAsync<ForbiddenException>(() => service.SetCoachesAsync(offenceCoordinator, receivers,
                new SetGroupMembersDto { UserIds = new List<int> { coach.Id } }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetCoachesAsync(headCoach, receivers,
                new SetGroupMembersDto { UserIds = new List<int> { player.Id } }));   // a player is not a coach

            Assert.True(await service.SetCoachesAsync(headCoach, receivers, new SetGroupMembersDto { UserIds = new List<int> { coach.Id } }));

            // The coach now has the group, which is what the permission rules look at
            var assigned = await _world.AsAsync(coach.Id);
            Assert.True(assigned.IsInGroup(receivers));
        }

        [Fact]
        public async Task SetPlayers_PutsAPlayerInSeveralGroups_AndRefusesPeopleWhoAreNotPlayersOfTheTeam()
        {
            var headCoach = await _world.AddTeamAsync();
            var coach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach);
            var player = await _world.AddPlayerAsync(headCoach, "Kim", "Receivers");
            var otherTeamCoach = await _world.AddTeamAsync("Test Bears");
            var otherTeamPlayer = await _world.AddPlayerAsync(otherTeamCoach, "Stone");
            int defensiveBacks = await _world.GroupIdAsync(headCoach, "Defensive Backs");

            using var context = _world.NewContext();
            var service = _world.Groups(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetPlayersAsync(headCoach, defensiveBacks,
                new SetGroupMembersDto { UserIds = new List<int> { coach.Id } }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetPlayersAsync(headCoach, defensiveBacks,
                new SetGroupMembersDto { UserIds = new List<int> { otherTeamPlayer.Id } }));

            Assert.True(await service.SetPlayersAsync(headCoach, defensiveBacks, new SetGroupMembersDto { UserIds = new List<int> { player.Id } }));

            var twoWayPlayer = await _world.AsAsync(player.Id);
            Assert.Equal(2, twoWayPlayer.Groups.Count);
            Assert.Contains(twoWayPlayer.Groups, g => g.Unit == TeamUnit.Offence);
            Assert.Contains(twoWayPlayer.Groups, g => g.Unit == TeamUnit.Defence);
        }

        [Fact]
        public async Task SetPlayers_RemovingAPlayerWhoIsOnThisWeeksChart_IsRejected()
        {
            var headCoach = await _world.AddTeamAsync();
            var placed = await _world.AddPlayerAsync(headCoach, "Placed", "Receivers");
            var unplaced = await _world.AddPlayerAsync(headCoach, "Unplaced", "Receivers");
            int receivers = await _world.GroupIdAsync(headCoach, "Receivers");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);
            int x = await _world.PositionIdAsync(headCoach, "X");

            using var context = _world.NewContext();
            await _world.DepthChart(context).AddPlayerAsync(headCoach, weekId, x, new AddPlacementDto { PlayerId = placed.Id });
            var service = _world.Groups(context);

            // Taking the placed player out of the group is refused...
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetPlayersAsync(headCoach, receivers,
                new SetGroupMembersDto { UserIds = new List<int> { unplaced.Id } }));
            Assert.Contains("depth chart", error.Message);

            // ...while taking out a player who is not on the chart is fine
            Assert.True(await service.SetPlayersAsync(headCoach, receivers, new SetGroupMembersDto { UserIds = new List<int> { placed.Id } }));
        }

        [Fact]
        public async Task AnotherTeamsGroupsAndPositions_CannotBeReached()
        {
            var tigersCoach = await _world.AddTeamAsync("Test Tigers");
            int tigersGroup = await _world.GroupIdAsync(tigersCoach, "Receivers");
            int tigersPosition = await _world.PositionIdAsync(tigersCoach, "X");
            var bearsCoach = await _world.AddTeamAsync("Test Bears");

            using var context = _world.NewContext();
            var service = _world.Groups(context);

            Assert.Null(await service.GetGroupAsync(bearsCoach, tigersGroup));
            Assert.Null(await service.GetPositionAsync(bearsCoach, tigersPosition));
            Assert.False(await service.RenameGroupAsync(bearsCoach, tigersGroup, new SavePositionGroupDto { Name = "Taken Over" }));
            Assert.False(await service.DeleteGroupAsync(bearsCoach, tigersGroup));
            Assert.False(await service.SetCoachesAsync(bearsCoach, tigersGroup, new SetGroupMembersDto()));
            Assert.False(await service.SetPlayersAsync(bearsCoach, tigersGroup, new SetGroupMembersDto()));
            Assert.False(await service.UpdatePositionAsync(bearsCoach, tigersPosition, new SavePositionDto { Code = "Q", Name = "Q" }));
            Assert.False(await service.DeletePositionAsync(bearsCoach, tigersPosition));

            // A position cannot be created inside another team's group
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePositionAsync(bearsCoach,
                new SavePositionDto { PositionGroupId = tigersGroup, Code = "Q", Name = "Q" }));

            Assert.Equal(9, (await service.GetGroupsAsync(bearsCoach)).Count);   // only their own nine
        }
    }
}
