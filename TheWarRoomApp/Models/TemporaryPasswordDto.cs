using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // Returned once, when an account is created or its password is reset.
    // Only the hash is stored, so the temporary password cannot be shown again later.
    public class TemporaryPasswordDto
    {
        public int UserId { get; set; }
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        [Display(Name = "Temporary password")]
        public string TemporaryPassword { get; set; } = string.Empty;
    }
}
