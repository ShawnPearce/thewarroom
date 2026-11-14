using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.DTOs
{
    // The team settings the head coach controls
    public class UpdateTeamDto
    {
        [Required(ErrorMessage = "Team name is required.")]
        [StringLength(100, ErrorMessage = "Team name can't exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        // While this is off, nobody can record attendance
        public bool AttendanceTrackingEnabled { get; set; }
    }
}
