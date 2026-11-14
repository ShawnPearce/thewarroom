using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The permission rules by role, unit and assigned groups (no database needed)
    public class AccessRulesTests
    {
        private static readonly PositionGroup Receivers = new PositionGroup { Id = 1, Unit = TeamUnit.Offence, Name = "Receivers" };
        private static readonly PositionGroup OffensiveLine = new PositionGroup { Id = 2, Unit = TeamUnit.Offence, Name = "Offensive Line" };
        private static readonly PositionGroup Linebackers = new PositionGroup { Id = 3, Unit = TeamUnit.Defence, Name = "Linebackers" };

        private static CurrentUser User(UserRole role, TeamUnit? unit = null, bool canEditDepthChart = true, params PositionGroup[] groups)
        {
            return new CurrentUser
            {
                Id = 1,
                TeamId = 1,
                Role = role,
                CoordinatorUnit = unit,
                CanEditDepthChart = canEditDepthChart,
                Groups = groups.Select(g => new CurrentUserGroup { Id = g.Id, Unit = g.Unit }).ToList()
            };
        }

        [Fact]
        public void HeadCoach_CanDoEverything()
        {
            var headCoach = User(UserRole.HeadCoach, canEditDepthChart: false);   // the switch never limits the head coach

            Assert.True(AccessRules.GroupIsInScope(headCoach, Receivers));
            Assert.True(AccessRules.GroupIsInScope(headCoach, Linebackers));
            Assert.True(AccessRules.CanEditDepthChart(headCoach, Linebackers));
            Assert.True(AccessRules.CanManageUnit(headCoach, TeamUnit.Defence));
            Assert.True(AccessRules.CanAssignCoaches(headCoach));
            Assert.True(AccessRules.CanCreateDepthChartWeek(headCoach));
            Assert.True(AccessRules.CanManageTeam(headCoach));
        }

        [Fact]
        public void Coordinator_ControlsTheGroupsOfTheirUnit()
        {
            var offenceCoordinator = User(UserRole.Coordinator, TeamUnit.Offence);

            Assert.True(AccessRules.CanEditDepthChart(offenceCoordinator, Receivers));
            Assert.True(AccessRules.CanEditDepthChart(offenceCoordinator, OffensiveLine));
            Assert.False(AccessRules.CanEditDepthChart(offenceCoordinator, Linebackers));

            Assert.True(AccessRules.CanManageUnit(offenceCoordinator, TeamUnit.Offence));
            Assert.False(AccessRules.CanManageUnit(offenceCoordinator, TeamUnit.Defence));

            Assert.True(AccessRules.CanCreateDepthChartWeek(offenceCoordinator));
            Assert.False(AccessRules.CanAssignCoaches(offenceCoordinator));
            Assert.False(AccessRules.CanManageTeam(offenceCoordinator));
        }

        [Fact]
        public void Coordinator_AlsoCoveringAGroupInAnotherUnit_HasThatGroupToo()
        {
            var offenceCoordinator = User(UserRole.Coordinator, TeamUnit.Offence, true, Linebackers);

            Assert.True(AccessRules.CanEditDepthChart(offenceCoordinator, Linebackers));

            // Coaching a group does not make them the coordinator of its unit
            Assert.False(AccessRules.CanManageUnit(offenceCoordinator, TeamUnit.Defence));
        }

        [Fact]
        public void PositionCoach_ManagesOnlyTheGroupsTheyAreAssignedTo()
        {
            var receiversCoach = User(UserRole.PositionCoach, null, true, Receivers);

            Assert.True(AccessRules.CanEditDepthChart(receiversCoach, Receivers));
            Assert.False(AccessRules.CanEditDepthChart(receiversCoach, OffensiveLine));   // same unit, but not their group
            Assert.False(AccessRules.CanEditDepthChart(receiversCoach, Linebackers));

            Assert.False(AccessRules.CanManageUnit(receiversCoach, TeamUnit.Offence));
            Assert.False(AccessRules.CanCreateDepthChartWeek(receiversCoach));
        }

        [Theory]
        [InlineData(UserRole.Coordinator)]
        [InlineData(UserRole.PositionCoach)]
        public void DepthChartSwitchOff_StopsACoachEvenInsideTheirScope(UserRole role)
        {
            var coach = User(role, TeamUnit.Offence, canEditDepthChart: false, Receivers);

            Assert.True(AccessRules.GroupIsInScope(coach, Receivers));
            Assert.False(AccessRules.CanEditDepthChart(coach, Receivers));
        }

        [Fact]
        public void Film_IsWorkedOnByEveryCoach_AndWatchedByPlayersItIsSharedWith()
        {
            var positionCoach = User(UserRole.PositionCoach, null, true, Linebackers);
            var receiver = User(UserRole.Player, null, true, Receivers);

            var nobody = new List<FilmAssignment>();
            var wholeTeam = new List<FilmAssignment> { new FilmAssignment { Audience = FilmAudience.Team } };
            var receivers = new List<FilmAssignment> { new FilmAssignment { Audience = FilmAudience.Group, PositionGroupId = Receivers.Id } };
            var linebackers = new List<FilmAssignment> { new FilmAssignment { Audience = FilmAudience.Group, PositionGroupId = Linebackers.Id } };
            var thisPlayer = new List<FilmAssignment> { new FilmAssignment { Audience = FilmAudience.Player, PlayerId = receiver.Id } };
            var anotherPlayer = new List<FilmAssignment> { new FilmAssignment { Audience = FilmAudience.Player, PlayerId = 99 } };

            // A coach works with all film, whoever it is shared with
            Assert.True(AccessRules.CanManageFilm(positionCoach));
            Assert.True(AccessRules.CanWatchCutup(positionCoach, nobody));

            // A player never works on film, and watches only what reaches them
            Assert.False(AccessRules.CanManageFilm(receiver));
            Assert.True(AccessRules.CanWatchCutup(receiver, wholeTeam));
            Assert.True(AccessRules.CanWatchCutup(receiver, receivers));
            Assert.True(AccessRules.CanWatchCutup(receiver, thisPlayer));
            Assert.False(AccessRules.CanWatchCutup(receiver, nobody));
            Assert.False(AccessRules.CanWatchCutup(receiver, linebackers));
            Assert.False(AccessRules.CanWatchCutup(receiver, anotherPlayer));
        }

        [Fact]
        public void Player_CanChangeNothing_EvenInTheirOwnGroup()
        {
            var player = User(UserRole.Player, null, true, Receivers);

            Assert.False(AccessRules.GroupIsInScope(player, Receivers));
            Assert.False(AccessRules.CanEditDepthChart(player, Receivers));
            Assert.False(AccessRules.CanManageUnit(player, TeamUnit.Offence));
            Assert.False(AccessRules.CanCreateDepthChartWeek(player));
            Assert.False(AccessRules.CanManageTeam(player));
        }
    }
}
