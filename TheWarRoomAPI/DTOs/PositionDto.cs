namespace TheWarRoomAPI.DTOs
{
    public class PositionDto
    {
        public int Id { get; set; }
        public int PositionGroupId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }
}
