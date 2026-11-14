using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // Says that a player belongs to a position group.
    // A player can belong to several groups, including groups on offence, defence and special teams.
    [Index(nameof(PlayerId), nameof(PositionGroupId), IsUnique = true)]
    public class PlayerGroupAssignment
    {
        public int Id { get; set; }

        // Foreign key for the PositionGroup entity
        public int PositionGroupId { get; set; }
        public PositionGroup? PositionGroup { get; set; }

        // Foreign key for the User entity (the player)
        public int PlayerId { get; set; }
        public User? Player { get; set; }
    }
}
