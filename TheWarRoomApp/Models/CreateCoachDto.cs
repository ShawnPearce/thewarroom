using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    public class CreateCoachDto
    {
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

        // Coordinator or PositionCoach
        public UserRole Role { get; set; }

        // Required when the role is Coordinator
        [Display(Name = "Unit")]
        public TeamUnit? CoordinatorUnit { get; set; }
    }
}
