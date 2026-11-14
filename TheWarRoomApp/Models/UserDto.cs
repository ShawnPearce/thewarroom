using System.ComponentModel.DataAnnotations;


namespace TheWarRoomApp.Models
{
    // An account on the team, as shown on the staff page and the account edit form
    public class UserDto
    {
        public int Id { get; set; }
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        // Left empty when a player is looking at the staff list
        public string? Email { get; set; }

        public UserRole Role { get; set; }
        [Display(Name = "Unit")]
        public TeamUnit? CoordinatorUnit { get; set; }
        [Display(Name = "Publish playbook changes without approval")]
        public bool CanPublishPlaybook { get; set; }
        [Display(Name = "Edit the depth chart")]
        public bool CanEditDepthChart { get; set; }
        [Display(Name = "Record attendance")]
        public bool CanRecordAttendance { get; set; }
        [Display(Name = "Has a temporary password")]
        public bool MustChangePassword { get; set; }
        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        // The position groups a coach is assigned to
        public List<GroupSummaryDto> Groups { get; set; } = new List<GroupSummaryDto>();
    }
}
