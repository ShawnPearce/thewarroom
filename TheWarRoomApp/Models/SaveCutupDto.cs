using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    public class SaveCutupDto
    {
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100, ErrorMessage = "Name can't exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "The description can't exceed 500 characters.")]
        public string? Description { get; set; }

        // Any day of the week the film is meant for; it is kept as that week's Monday. Optional.
        [Display(Name = "For the week of")]
        public DateOnly? ForWeekOf { get; set; }
    }
}
