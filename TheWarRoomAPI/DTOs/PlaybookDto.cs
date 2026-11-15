using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    public class PlaybookDto
    {
        public int Id { get; set; }
        public TeamUnit Unit { get; set; }
        public string Name { get; set; } = string.Empty;
        public int FormationCount { get; set; }
        public int PlayCount { get; set; }

        // The game the playbook is drawn for, and how many players a side that puts on the field (11 or 12)
        public FootballRules Rules { get; set; }
        public int PlayersPerSide { get; set; }

        // True when the playbook belongs to the game the team is not playing now: it can be read, but not changed
        public bool IsPutAway { get; set; }

        // What the current user may do in this playbook, so the page can show the right buttons.
        // CanPublish false with CanChange true means their changes wait for the head coach's approval.
        public bool CanChange { get; set; }
        public bool CanPublish { get; set; }
    }

    // A playbook with its formations
    public class PlaybookDetailDto : PlaybookDto
    {
        public List<FormationSummaryDto> Formations { get; set; } = new List<FormationSummaryDto>();
    }

    public class FormationSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DiagramJson { get; set; } = string.Empty;
        public int PlayCount { get; set; }

        // True (for coaches) when a change to this formation is waiting for approval
        public bool HasPendingChange { get; set; }
    }
}
