using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    public class UpdateTeamDto
    {
        [Required(ErrorMessage = "Team name is required.")]
        [StringLength(100, ErrorMessage = "Team name can't exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;
    }
}
