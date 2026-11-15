using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // What the editor on a video's page sends when a play is saved.
    // The times come from the player, so they are already in milliseconds.
    public class PlayEditModel
    {
        // 0 for a new play
        public int Id { get; set; }
        public int VideoId { get; set; }

        [Range(0, int.MaxValue)]
        public int StartMs { get; set; }

        [Range(0, int.MaxValue)]
        public int EndMs { get; set; }

        [StringLength(100, ErrorMessage = "The note can't exceed 100 characters.")]
        public string? Title { get; set; }

        // The tag. Leaving the play type empty means the play is not tagged.
        public PlayType? PlayType { get; set; }
        public int? OffenceTeamId { get; set; }

        [Range(1, 4, ErrorMessage = "The down must be between 1 and 4.")]
        public int? Down { get; set; }

        [Range(1, 99, ErrorMessage = "The distance must be between 1 and 99 yards.")]
        public int? Distance { get; set; }
    }
}
