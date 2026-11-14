using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.DTOs
{
    public class SavePositionDto
    {
        // Used when the position is created; a position stays in its group afterwards
        public int PositionGroupId { get; set; }

        [Required(ErrorMessage = "Code is required.")]
        [StringLength(6, ErrorMessage = "Code can't exceed 6 characters.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Position name is required.")]
        [StringLength(50, ErrorMessage = "Position name can't exceed 50 characters.")]
        public string Name { get; set; } = string.Empty;
    }
}
