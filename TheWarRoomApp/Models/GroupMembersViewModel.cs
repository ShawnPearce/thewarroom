namespace TheWarRoomApp.Models
{
    // The page where a group's coaches or players are chosen from a list of checkboxes
    public class GroupMembersViewModel
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public TeamUnit Unit { get; set; }

        // "Coaches" or "Players": the same page serves both
        public string Kind { get; set; } = string.Empty;

        public List<MemberChoice> Choices { get; set; } = new List<MemberChoice>();
    }

    public class MemberChoice
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // A short note next to the name, for example a role or the player's other groups
        public string Detail { get; set; } = string.Empty;

        public bool IsSelected { get; set; }
    }
}
