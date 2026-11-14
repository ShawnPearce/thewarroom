using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    public class SavePositionGroupDto
    {
        // Used when the group is created; a group's unit does not change afterwards
        public TeamUnit Unit { get; set; }

        [Required(ErrorMessage = "Group name is required.")]
        [StringLength(50, ErrorMessage = "Group name can't exceed 50 characters.")]
        public string Name { get; set; } = string.Empty;
    }
}
