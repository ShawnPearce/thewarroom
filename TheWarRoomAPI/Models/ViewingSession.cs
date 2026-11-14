using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // One visit by a player to a film page or a playbook page, and the viewing time counted on it.
    // The page reports its active time as it goes; the server decides how much of it is credited
    // (see StudyTimeService). This is separate from login time, which comes from LoginSession.
    [Index(nameof(UserId), nameof(PageKey), IsUnique = true)]
    public class ViewingSession
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Foreign key for the User entity (the player)
        public int UserId { get; set; }
        public User? User { get; set; }

        public ViewingKind Kind { get; set; }

        // What was being viewed: a cutup when Kind is Film, a playbook when Kind is Playbook
        public int? CutupId { get; set; }
        public Cutup? Cutup { get; set; }

        public int? PlaybookId { get; set; }
        public Playbook? Playbook { get; set; }

        // Made up by the page when it is opened, so that all of its reports are added to this row
        [Required]
        [StringLength(40)]
        public string PageKey { get; set; } = string.Empty;

        // Foreign key for the LoginSession entity (the sign-in the viewing happened in, when known)
        public int? LoginSessionId { get; set; }
        public LoginSession? LoginSession { get; set; }

        public DateTime StartedAt { get; set; }

        // The last moment time was credited to this row
        public DateTime LastCreditAt { get; set; }

        // The credited viewing time, in milliseconds
        public int ActiveMs { get; set; }
    }
}
