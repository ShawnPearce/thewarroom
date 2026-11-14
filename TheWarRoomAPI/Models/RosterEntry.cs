using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.Models
{
    // The football details of one player. Every player account has exactly one roster entry.
    public class RosterEntry
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Foreign key for the User entity (the player's account)
        public int UserId { get; set; }

        //Navigation property to represent the One-to-One relationship with User
        public User? User { get; set; }

        [Range(0, 99, ErrorMessage = "Jersey number must be between 0 and 99.")]
        public int? JerseyNumber { get; set; }

        [Range(9, 12, ErrorMessage = "Grade must be between 9 and 12.")]
        public int? Grade { get; set; }

        [Range(48, 90, ErrorMessage = "Height must be between 48 and 90 inches.")]
        public int? HeightInches { get; set; }

        [Range(80, 400, ErrorMessage = "Weight must be between 80 and 400 pounds.")]
        public int? WeightLbs { get; set; }

        public RosterStatus Status { get; set; } = RosterStatus.Active;
    }
}
