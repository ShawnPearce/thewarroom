using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.DTOs
{
    public class CreatePlayerDto
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

        [Range(0, 99, ErrorMessage = "Jersey number must be between 0 and 99.")]
        public int? JerseyNumber { get; set; }

        [Range(9, 12, ErrorMessage = "Grade must be between 9 and 12.")]
        public int? Grade { get; set; }

        [Range(48, 90, ErrorMessage = "Height must be between 48 and 90 inches.")]
        public int? HeightInches { get; set; }

        [Range(80, 400, ErrorMessage = "Weight must be between 80 and 400 pounds.")]
        public int? WeightLbs { get; set; }

        // The position groups the player starts in
        public List<int> GroupIds { get; set; } = new List<int>();
    }
}
