using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.DTOs
{
    public class SaveFormationDto
    {
        // Used when the formation is created; a formation stays in its playbook afterwards
        public int PlaybookId { get; set; }

        [Required(ErrorMessage = "Formation name is required.")]
        [StringLength(60, ErrorMessage = "Formation name can't exceed 60 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "The diagram is missing.")]
        public string DiagramJson { get; set; } = string.Empty;

        // When an existing formation is changed: the version the drawing was started from.
        // The change is refused if somebody else changed the formation in the meantime. Optional.
        public int? Version { get; set; }
    }
}
