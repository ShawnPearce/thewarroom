using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // The signed-in user's own account, as the API currently sees it
    public class CurrentUserDto
    {
        public int Id { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;

        // The game the team plays: American or Canadian football
        public FootballRules Rules { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public TeamUnit? CoordinatorUnit { get; set; }
        public bool CanPublishPlaybook { get; set; }
        public bool CanEditDepthChart { get; set; }
        public bool CanRecordAttendance { get; set; }
        public bool MustChangePassword { get; set; }
    }
}
