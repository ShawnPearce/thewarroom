namespace TheWarRoomAPI.DTOs
{
    // The complete list of coaches (or players) a group should have after the change
    public class SetGroupMembersDto
    {
        public List<int> UserIds { get; set; } = new List<int>();
    }
}
