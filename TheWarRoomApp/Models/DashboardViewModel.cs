namespace TheWarRoomApp.Models
{
    // What the dashboard shows after signing in
    public class DashboardViewModel
    {
        public CurrentUserDto Me { get; set; } = new CurrentUserDto();

        // This week's depth chart, when the team has one
        public DepthChartWeekDto? Week { get; set; }

        // A player's own spots on this week's chart
        public List<MyPlacementDto> MyPlacements { get; set; } = new List<MyPlacementDto>();

        // The next few events on the schedule
        public List<ScheduledEventDto> NextEvents { get; set; } = new List<ScheduledEventDto>();

        // The playbooks this user can open
        public List<PlaybookDto> Playbooks { get; set; } = new List<PlaybookDto>();

        // For coaches: the playbook changes that are waiting for the head coach
        public List<ApprovalRequestDto> WaitingChanges { get; set; } = new List<ApprovalRequestDto>();

        // Film: a player's shared cutups, or all of the team's cutups for a coach (the latest first)
        public List<CutupDto> Cutups { get; set; } = new List<CutupDto>();
    }
}
