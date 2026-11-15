using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // The register form: the fields of RegisterTeamDto plus the password typed a second time
    public class RegisterTeamViewModel
    {
        [Required(ErrorMessage = "Team name is required.")]
        [StringLength(100, ErrorMessage = "Team name can't exceed 100 characters.")]
        [Display(Name = "Team name")]
        public string TeamName { get; set; } = string.Empty;

        // The game the team plays; the head coach can change it later in the team's settings
        [Display(Name = "Game")]
        public FootballRules Rules { get; set; } = FootballRules.American;

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50, ErrorMessage = "First name can't exceed 50 characters.")]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50, ErrorMessage = "Last name can't exceed 50 characters.")]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Type the password again.")]
        [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
