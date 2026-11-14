namespace TheWarRoomAPI.Models
{
    // Says that a cutup is shared with the whole team, with one position group, or with one player.
    // A cutup can have several: for example two groups and three extra players.
    public class FilmAssignment
    {
        public int Id { get; set; }

        // Foreign key for the Cutup entity
        public int CutupId { get; set; }
        public Cutup? Cutup { get; set; }

        public FilmAudience Audience { get; set; }

        // Foreign key for the PositionGroup entity (filled when Audience is Group)
        public int? PositionGroupId { get; set; }
        public PositionGroup? PositionGroup { get; set; }

        // Foreign key for the User entity (the player; filled when Audience is Player)
        public int? PlayerId { get; set; }
        public User? Player { get; set; }

        // Foreign key for the User entity (the coach who shared it)
        public int AssignedById { get; set; }
        public User? AssignedBy { get; set; }

        public DateTime AssignedAt { get; set; }
    }
}
