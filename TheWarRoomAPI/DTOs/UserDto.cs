using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // An account on the team, as shown on the staff page and the account edit form
    public class UserDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // Left empty when a player is looking at the staff list
        public string? Email { get; set; }

        public UserRole Role { get; set; }
        public TeamUnit? CoordinatorUnit { get; set; }
        public bool CanPublishPlaybook { get; set; }
        public bool CanEditDepthChart { get; set; }
        public bool CanRecordAttendance { get; set; }
        public bool MustChangePassword { get; set; }
        public bool IsActive { get; set; }

        // The position groups a coach is assigned to
        public List<GroupSummaryDto> Groups { get; set; } = new List<GroupSummaryDto>();
    }
}
