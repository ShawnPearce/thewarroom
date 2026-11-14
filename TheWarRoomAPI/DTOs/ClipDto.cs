using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // One play cut out of a video. Times are milliseconds from the start of the video.
    public class ClipDto
    {
        public int Id { get; set; }

        public int VideoId { get; set; }
        public string VideoTitle { get; set; } = string.Empty;

        // The game the video belongs to, so a list of clips from several games can say where each is from
        public int GameId { get; set; }
        public DateOnly GameDate { get; set; }
        public string Matchup { get; set; } = string.Empty;

        public int StartMs { get; set; }
        public int EndMs { get; set; }
        public string? Title { get; set; }

        // Empty when the play has not been tagged
        public PlayTagDto? Tag { get; set; }

        // How many cutups use this clip. A clip that is used cannot be deleted.
        public int CutupCount { get; set; }
    }

    public class PlayTagDto
    {
        public PlayType PlayType { get; set; }
        public int Down { get; set; }
        public int Distance { get; set; }
        public int OffenceTeamId { get; set; }
        public string OffenceTeamName { get; set; } = string.Empty;
    }
}
