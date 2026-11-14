using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    public class SavePlayDto
    {
        // Used when the play is created; a play stays under its formation afterwards
        public int FormationId { get; set; }

        [Required(ErrorMessage = "Play name is required.")]
        [StringLength(60, ErrorMessage = "Play name can't exceed 60 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "The diagram is missing.")]
        public string DiagramJson { get; set; } = string.Empty;

        // When an existing play is changed: the version the drawing was started from.
        // The change is refused if somebody else changed the play in the meantime. Optional.
        public int? Version { get; set; }

        [StringLength(1000, ErrorMessage = "Notes can't exceed 1000 characters.")]
        public string? Notes { get; set; }
    }
}
