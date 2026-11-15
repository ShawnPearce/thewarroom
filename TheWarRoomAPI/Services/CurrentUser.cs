using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // The user a request is acting as. It is built fresh from the database on every request
    // (see CurrentUserFilter), so a changed role or permission takes effect immediately.
    // Every service method that checks permissions receives it as its first argument.
    public class CurrentUser
    {
        public int Id { get; set; }
        public int TeamId { get; set; }

        // The game the user's team plays. It decides which positions and playbooks the team is working with.
        public FootballRules Rules { get; set; } = FootballRules.American;

        public UserRole Role { get; set; }
        public TeamUnit? CoordinatorUnit { get; set; }
        public bool CanPublishPlaybook { get; set; }
        public bool CanEditDepthChart { get; set; }
        public bool CanRecordAttendance { get; set; }

        // The position groups this user is assigned to: the groups a coach coaches, or the groups a player is in
        public List<CurrentUserGroup> Groups { get; set; } = new List<CurrentUserGroup>();

        // The sign-in this request belongs to; empty when the caller sent no session token (Swagger)
        public int? LoginSessionId { get; set; }

        public bool IsHeadCoach => Role == UserRole.HeadCoach;
        public bool IsCoordinator => Role == UserRole.Coordinator;
        public bool IsCoach => Role != UserRole.Player;
        public bool IsPlayer => Role == UserRole.Player;

        public bool IsInGroup(int positionGroupId)
        {
            return Groups.Any(g => g.Id == positionGroupId);
        }
    }

    // One of the current user's position groups, with the unit it belongs to
    public class CurrentUserGroup
    {
        public int Id { get; set; }
        public TeamUnit Unit { get; set; }
    }
}
