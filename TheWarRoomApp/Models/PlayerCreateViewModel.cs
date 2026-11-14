namespace TheWarRoomApp.Models
{
    // The add-player form, with the position groups to choose from
    public class PlayerCreateViewModel
    {
        public CreatePlayerDto Player { get; set; } = new CreatePlayerDto();

        public List<PositionGroupDto> Groups { get; set; } = new List<PositionGroupDto>();
    }
}
