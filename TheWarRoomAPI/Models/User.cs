using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.Models
{
    // An account: a coach or a player. Each account belongs to one team.
    public class User
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }

        //Navigation property to represent the relationship with Team
        public Team? Team { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        // Only the hash is stored, never the password itself
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        public UserRole Role { get; set; }

        // Set only for coordinators: the unit they run
        public TeamUnit? CoordinatorUnit { get; set; }

        // Permission switches the head coach controls for each coach
        public bool CanPublishPlaybook { get; set; }
        public bool CanEditDepthChart { get; set; } = true;
        public bool CanRecordAttendance { get; set; } = true;

        // True while the account still has the temporary password the head coach was given
        public bool MustChangePassword { get; set; }

        // Accounts are deactivated instead of deleted so their history stays intact
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
    }
}
