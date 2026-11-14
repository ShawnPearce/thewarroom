using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // Says that a coach coaches a position group. A coach can have several groups.
    [Index(nameof(CoachId), nameof(PositionGroupId), IsUnique = true)]
    public class CoachGroupAssignment
    {
        public int Id { get; set; }

        // Foreign key for the PositionGroup entity
        public int PositionGroupId { get; set; }
        public PositionGroup? PositionGroup { get; set; }

        // Foreign key for the User entity (the coach)
        public int CoachId { get; set; }
        public User? Coach { get; set; }
    }
}
