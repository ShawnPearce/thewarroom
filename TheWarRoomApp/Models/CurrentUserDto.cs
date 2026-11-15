using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // The signed-in user's own account, as returned by GET api/Auth/me
    public class CurrentUserDto
    {
        public int Id { get; set; }
        public int TeamId { get; set; }

        [Display(Name = "Team")]
        public string TeamName { get; set; } = string.Empty;

        // The game the team plays: American or Canadian football
        public FootballRules Rules { get; set; }

        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Last name")]
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
