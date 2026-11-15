using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.Models
{
    // A team is a private workspace. Every other record belongs to exactly one team.
    public class Team
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // The head coach switches attendance tracking on or off for the whole team
        public bool AttendanceTrackingEnabled { get; set; }

        // The game the team plays: American (11 a side) or Canadian (12 a side). The head coach chooses it.
        // It decides which positions the depth chart shows and which playbooks the team works in.
        public FootballRules Rules { get; set; } = FootballRules.American;

        public DateTime CreatedAt { get; set; }

        //Navigation property to represent the One-to-Many relationship with User
        public List<User>? Users { get; set; }
    }
}
