namespace TheWarRoomApp.Models
{
    // Everyone a cutup should be shared with after the change
    public class SetCutupSharingDto
    {
        public bool WholeTeam { get; set; }
        public List<int> GroupIds { get; set; } = new List<int>();
        public List<int> PlayerIds { get; set; } = new List<int>();
    }
}
