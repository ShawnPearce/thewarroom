namespace TheWarRoomAPI.DTOs
{
    // The clips to add to a cutup, in the order they should be played
    public class AddCutupClipsDto
    {
        public List<int> ClipIds { get; set; } = new List<int>();
    }
}
