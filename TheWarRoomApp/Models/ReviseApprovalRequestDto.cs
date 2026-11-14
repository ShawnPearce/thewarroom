using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // The requester's new version of a change that is still waiting for approval
    public class ReviseApprovalRequestDto
    {
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(60, ErrorMessage = "Name can't exceed 60 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "The diagram is missing.")]
        public string DiagramJson { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Notes can't exceed 1000 characters.")]
        public string? Notes { get; set; }
    }
}
