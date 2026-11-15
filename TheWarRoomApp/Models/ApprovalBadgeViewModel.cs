namespace TheWarRoomApp.Models
{
    // The number beside "Approvals" in the menu, and what it counts (read out to screen readers)
    public class ApprovalBadgeViewModel
    {
        public int Count { get; set; }
        public string Meaning { get; set; } = string.Empty;
    }
}
