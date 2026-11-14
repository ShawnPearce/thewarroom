using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.DTOs
{
    public class SaveCutupDto
    {
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100, ErrorMessage = "Name can't exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "The description can't exceed 500 characters.")]
        public string? Description { get; set; }

        // Any day of the week the film is meant for; it is kept as that week's Monday. Optional.
        public DateOnly? ForWeekOf { get; set; }
    }
}
