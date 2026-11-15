using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // The team settings the head coach controls
    public class UpdateTeamDto
    {
        [Required(ErrorMessage = "Team name is required.")]
        [StringLength(100, ErrorMessage = "Team name can't exceed 100 characters.")]
        [Display(Name = "Team name")]
        public string Name { get; set; } = string.Empty;

        // While this is off, nobody can record attendance
        [Display(Name = "Attendance tracking")]
        public bool AttendanceTrackingEnabled { get; set; }

        // American football (11 a side) or Canadian football (12 a side).
        // Changing it changes the positions and the playbooks the team works with.
        [Display(Name = "Game")]
        public FootballRules Rules { get; set; } = FootballRules.American;
    }
}
