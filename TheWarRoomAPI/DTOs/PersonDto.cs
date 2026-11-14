namespace TheWarRoomAPI.DTOs
{
    // A coach or player as a name in a list. JerseyNumber is filled for players only.
    public class PersonDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? JerseyNumber { get; set; }
    }
}
