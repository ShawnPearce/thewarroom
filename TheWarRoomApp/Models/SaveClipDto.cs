using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    public class SaveClipDto
    {
        // Used when the clip is created; a clip stays in its video afterwards
        public int VideoId { get; set; }

        // Milliseconds from the start of the video
        [Range(0, int.MaxValue, ErrorMessage = "The start cannot be before the beginning of the video.")]
        public int StartMs { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "The end cannot be before the beginning of the video.")]
        public int EndMs { get; set; }

        [StringLength(100, ErrorMessage = "The note can't exceed 100 characters.")]
        public string? Title { get; set; }

        // Leave empty for a play that is not tagged
        public SavePlayTagDto? Tag { get; set; }
    }

    public class SavePlayTagDto
    {
        public PlayType PlayType { get; set; }

        [Range(1, 4, ErrorMessage = "The down must be between 1 and 4.")]
        public int Down { get; set; }

        [Range(1, 99, ErrorMessage = "The distance must be between 1 and 99 yards.")]
        public int Distance { get; set; }

        // The team on offence: one of the two teams in the game
        public int OffenceTeamId { get; set; }
    }
}
