using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // The edit-player form. The head coach can also change the name and email;
    // a coordinator edits the football details only.
    public class PlayerEditViewModel
    {
        public int PlayerId { get; set; }

        public bool CanEditAccount { get; set; }

        [StringLength(50, ErrorMessage = "First name can't exceed 50 characters.")]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Last name can't exceed 50 characters.")]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(256)]
        public string? Email { get; set; }

        [Range(0, 99, ErrorMessage = "Jersey number must be between 0 and 99.")]
        [Display(Name = "Jersey number")]
        public int? JerseyNumber { get; set; }

        [Range(9, 12, ErrorMessage = "Grade must be between 9 and 12.")]
        public int? Grade { get; set; }

        [Range(48, 90, ErrorMessage = "Height must be between 48 and 90 inches.")]
        [Display(Name = "Height (inches)")]
        public int? HeightInches { get; set; }

        [Range(80, 400, ErrorMessage = "Weight must be between 80 and 400 pounds.")]
        [Display(Name = "Weight (lbs)")]
        public int? WeightLbs { get; set; }

        public RosterStatus Status { get; set; }
    }
}
