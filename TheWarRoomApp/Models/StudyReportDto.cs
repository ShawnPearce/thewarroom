using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // What a film or playbook page sends while a player is on it: how long the player
    // has been actively viewing since the page's previous report.
    public class StudyReportDto
    {
        public ViewingKind Kind { get; set; }

        // The cutup being watched (film) or the playbook being read (playbook)
        public int? CutupId { get; set; }
        public int? PlaybookId { get; set; }

        // Made up by the page when it is opened; every report from that page carries the same key
        [Required(ErrorMessage = "The page key is missing.")]
        [StringLength(40, MinimumLength = 8, ErrorMessage = "The page key must be 8 to 40 characters.")]
        public string PageKey { get; set; } = string.Empty;

        // Active viewing time since the previous report, in milliseconds
        [Range(1, 600_000, ErrorMessage = "The active time must be between 1 millisecond and 10 minutes.")]
        public int ActiveMs { get; set; }
    }

    // How much of a report was counted
    public class StudyCreditDto
    {
        public int CreditedMs { get; set; }

        // Everything counted on this page so far
        public int PageTotalMs { get; set; }
    }
}
