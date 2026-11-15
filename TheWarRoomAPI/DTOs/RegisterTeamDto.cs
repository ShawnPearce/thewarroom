using System.ComponentModel.DataAnnotations;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // Registering a team creates the team and its head coach account together
    public class RegisterTeamDto
    {
        [Required(ErrorMessage = "Team name is required.")]
        [StringLength(100, ErrorMessage = "Team name can't exceed 100 characters.")]
        public string TeamName { get; set; } = string.Empty;

        // The game the team plays. Left out, it is American football; the head coach can change it later.
        [EnumDataType(typeof(FootballRules), ErrorMessage = "Choose American or Canadian football.")]
        public FootballRules? Rules { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50, ErrorMessage = "First name can't exceed 50 characters.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50, ErrorMessage = "Last name can't exceed 50 characters.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = string.Empty;
    }
}
