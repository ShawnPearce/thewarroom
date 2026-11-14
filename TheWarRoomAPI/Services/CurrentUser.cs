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
        public UserRole Role { get; set; }
        public TeamUnit? CoordinatorUnit { get; set; }
        public bool CanPublishPlaybook { get; set; }
        public bool CanEditDepthChart { get; set; }
        public bool CanRecordAttendance { get; set; }

        // The sign-in this request belongs to; empty when the caller sent no session token (Swagger)
        public int? LoginSessionId { get; set; }

        public bool IsHeadCoach => Role == UserRole.HeadCoach;
        public bool IsCoach => Role != UserRole.Player;
        public bool IsPlayer => Role == UserRole.Player;
    }
}
