using System.ComponentModel.DataAnnotations;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // The account details the head coach can change for any coach or player
    public class UpdateUserDto
    {
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

        public UserRole Role { get; set; }

        public TeamUnit? CoordinatorUnit { get; set; }
    }
}
