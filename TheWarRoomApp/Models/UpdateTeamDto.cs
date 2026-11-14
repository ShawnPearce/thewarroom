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
    }
}
