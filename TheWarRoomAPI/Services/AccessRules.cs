using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // The permission rules that depend on a user's role, unit and assigned groups.
    // They are kept together here so every service applies exactly the same rules,
    // and so they can be tested on their own.
    //
    //   Head coach      - everything on the team
    //   Coordinator     - the groups in their unit, plus any group they are assigned to
    //   Position coach  - the groups they are assigned to
    //   Player          - can view, never edit
    public static class AccessRules
    {
        // Is this group inside the part of the team the coach is responsible for?
        public static bool GroupIsInScope(CurrentUser user, PositionGroup group)
        {
            if (!user.IsCoach)
            {
                return false;
            }

            if (user.IsHeadCoach)
            {
                return true;
            }

            if (user.IsCoordinator && user.CoordinatorUnit == group.Unit)
            {
                return true;
            }

            return user.IsInGroup(group.Id);
        }

        // Adding, renaming and removing groups and positions, and choosing a group's players:
        // the head coach, or the coordinator of the group's unit.
        public static bool CanManageUnit(CurrentUser user, TeamUnit unit)
        {
            return user.IsHeadCoach || (user.IsCoordinator && user.CoordinatorUnit == unit);
        }

        // Choosing which coaches coach a group: the head coach only.
        public static bool CanAssignCoaches(CurrentUser user)
        {
            return user.IsHeadCoach;
        }

        // Changing the depth chart at a position of this group:
        // the group must be in the coach's scope, and the head coach must not have switched the permission off.
        public static bool CanEditDepthChart(CurrentUser user, PositionGroup group)
        {
            if (user.IsHeadCoach)
            {
                return true;
            }

            return user.IsCoach && user.CanEditDepthChart && GroupIsInScope(user, group);
        }

        // Creating a new week's depth chart: the head coach and coordinators.
        public static bool CanCreateDepthChartWeek(CurrentUser user)
        {
            return user.IsHeadCoach || user.IsCoordinator;
        }

        // Adding, changing and removing schedule events: the head coach and coordinators.
        public static bool CanManageSchedule(CurrentUser user)
        {
            return user.IsHeadCoach || user.IsCoordinator;
        }

        // Recording attendance: the head coach, or a coach whose attendance permission has not been switched off.
        // Whether tracking is switched on for the team at all is a separate rule (see AttendanceService).
        public static bool CanRecordAttendance(CurrentUser user)
        {
            return user.IsHeadCoach || (user.IsCoach && user.CanRecordAttendance);
        }

        // ---------- Playbook ----------

        // Reading a unit's playbook: every coach; a player only for the units of the groups they are in.
        public static bool CanReadPlaybook(CurrentUser user, TeamUnit unit)
        {
            return user.IsCoach || user.Groups.Any(g => g.Unit == unit);
        }

        // Proposing changes to a unit's playbook (the "authorized playbook scope"):
        // the head coach, the coordinator of that unit, and any coach assigned to a group of that unit.
        public static bool CanChangePlaybook(CurrentUser user, TeamUnit unit)
        {
            if (!user.IsCoach)
            {
                return false;
            }

            if (user.IsHeadCoach)
            {
                return true;
            }

            if (user.IsCoordinator && user.CoordinatorUnit == unit)
            {
                return true;
            }

            return user.Groups.Any(g => g.Unit == unit);
        }

        // Publishing a change without waiting for approval: the head coach always;
        // another coach only inside their scope and only when the head coach has granted the right.
        public static bool CanPublishPlaybook(CurrentUser user, TeamUnit unit)
        {
            return user.IsHeadCoach || (user.CanPublishPlaybook && CanChangePlaybook(user, unit));
        }

        // Approving or rejecting playbook changes: the head coach only.
        public static bool CanReviewPlaybookChanges(CurrentUser user)
        {
            return user.IsHeadCoach;
        }

        // Creating accounts, changing roles and permissions, deactivating, resetting passwords,
        // team settings, deleting a week: the head coach only.
        public static bool CanManageTeam(CurrentUser user)
        {
            return user.IsHeadCoach;
        }
    }
}
