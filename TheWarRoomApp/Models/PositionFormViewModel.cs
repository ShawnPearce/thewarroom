namespace TheWarRoomApp.Models
{
    // The add/edit position form, with the group it belongs to
    public class PositionFormViewModel
    {
        public int Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public SavePositionDto Position { get; set; } = new SavePositionDto();
    }
}
