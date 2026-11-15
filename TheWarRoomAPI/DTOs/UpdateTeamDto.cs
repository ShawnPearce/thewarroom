using System.ComponentModel.DataAnnotations;
using TheWarRoomAPI.Models;

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

        // The game the team plays. Changing it changes the positions and playbooks the team works with.
        // Left out, the game stays as it is.
        [EnumDataType(typeof(FootballRules), ErrorMessage = "Choose American or Canadian football.")]
        public FootballRules? Rules { get; set; }
    }
}
