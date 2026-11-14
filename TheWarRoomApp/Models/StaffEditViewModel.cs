using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // The edit page for one coach: the account form, plus what the page shows around it
    public class StaffEditViewModel
    {
        public int Id { get; set; }

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

        public UserRole Role { get; set; }

        [Display(Name = "Unit")]
        public TeamUnit? CoordinatorUnit { get; set; }

        // Shown on the page, not edited through the account form
        public bool IsHeadCoachAccount { get; set; }
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public List<GroupSummaryDto> Groups { get; set; } = new List<GroupSummaryDto>();
        public UpdatePermissionsDto Permissions { get; set; } = new UpdatePermissionsDto();
    }
}
