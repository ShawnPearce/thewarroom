using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.Models
{
    // One sign-in, from login until logout or the last request that was seen.
    // Login time in the activity report is calculated from these rows.
    public class LoginSession
    {
        public int Id { get; set; }

        // Foreign key for the User entity
        public int UserId { get; set; }

        //Navigation property to represent the relationship with User
        public User? User { get; set; }

        // Random value kept in the MVC app's login cookie; it identifies this sign-in
        [Required]
        [StringLength(64)]
        public string Token { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; }

        // Updated as requests arrive, at most once a minute
        public DateTime LastSeenAt { get; set; }

        // Set when the user signs out; empty when the session simply went quiet
        public DateTime? EndedAt { get; set; }
    }
}
