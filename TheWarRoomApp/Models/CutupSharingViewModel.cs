namespace TheWarRoomApp.Models
{
    // The page where a coach chooses who a cutup is shared with
    public class CutupSharingViewModel
    {
        public int CutupId { get; set; }
        public string CutupName { get; set; } = string.Empty;

        public bool WholeTeam { get; set; }

        // The team's position groups and active players, each with a tick when the cutup is shared with them
        public List<MemberChoice> Groups { get; set; } = new List<MemberChoice>();
        public List<MemberChoice> Players { get; set; } = new List<MemberChoice>();
    }
}
