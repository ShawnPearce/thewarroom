using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // An edited film, as a row in the list
    public class CutupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        // The Monday of the week this film is meant for, when the coach said so
        [Display(Name = "For the week of")]
        public DateOnly? ForWeekOf { get; set; }

        public CutupStatus Status { get; set; }

        // True when the cutup has been made into a video that can be played
        public bool HasVideo { get; set; }

        public int PlayCount { get; set; }

        // The length of all its plays one after another, in milliseconds
        public int DurationMs { get; set; }

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // For coaches: who it is shared with, in words ("Whole team", "Receivers", "#7 Jalen Carter")
        public List<string> SharedWith { get; set; } = new List<string>();

        // True when the current user may change, share and delete it (coaches)
        public bool CanManage { get; set; }
    }

    // A cutup with its plays in order
    public class CutupDetailDto : CutupDto
    {
        public List<CutupPlayDto> Plays { get; set; } = new List<CutupPlayDto>();

        // For coaches: who it is shared with. Empty for players.
        public CutupSharingDto? Sharing { get; set; }
    }

    // One play of a cutup, at its place in the order
    public class CutupPlayDto
    {
        public int Id { get; set; }
        public int Position { get; set; }
        public string? Title { get; set; }

        // The game the play comes from
        public DateOnly GameDate { get; set; }
        public string Matchup { get; set; } = string.Empty;

        // Where the play is in its source video
        public int StartMs { get; set; }
        public int EndMs { get; set; }

        // Where the play starts in the cutup: the length of all the plays before it
        public int OffsetMs { get; set; }
        public int DurationMs { get; set; }

        // For coaches only: the clip it was taken from and how that clip is tagged
        public int? ClipId { get; set; }
        public PlayTagDto? Tag { get; set; }
    }

    public class CutupSharingDto
    {
        public bool WholeTeam { get; set; }
        public List<GroupSummaryDto> Groups { get; set; } = new List<GroupSummaryDto>();
        public List<PersonDto> Players { get; set; } = new List<PersonDto>();
    }
}
