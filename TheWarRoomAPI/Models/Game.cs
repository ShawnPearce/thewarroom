using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.Models
{
    // A game the team has film of. Either side can be the team itself or an opponent.
    public class Game
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        public DateOnly GameDate { get; set; }

        // Foreign keys for the ScoutedTeam entity: the two teams that played
        public int HomeTeamId { get; set; }
        public ScoutedTeam? HomeTeam { get; set; }

        public int AwayTeamId { get; set; }
        public ScoutedTeam? AwayTeam { get; set; }

        // For example "Week 3" or "Quarter-final"
        [StringLength(60)]
        public string? Label { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        // Foreign key for the User entity (the coach who added the game)
        public int CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        //Navigation property to represent the One-to-Many relationship with Video
        public List<Video>? Videos { get; set; }
    }
}
