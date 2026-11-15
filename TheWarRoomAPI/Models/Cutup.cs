using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.Models
{
    // An edited film: chosen plays in a chosen order, shared with the team, with groups or with players.
    public class Cutup
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // The week this film is meant for (its Monday), when the coach says so
        public DateOnly? ForWeekOf { get; set; }

        public CutupStatus Status { get; set; }

        // Where the finished video is in film storage; empty until it has been made
        [StringLength(300)]
        public string? StorageKey { get; set; }

        // Why the video could not be made, when Status is Failed
        [StringLength(300)]
        public string? FailureReason { get; set; }

        // The length of the finished video in milliseconds, measured after it was made
        public int? DurationMs { get; set; }

        // How far the making of the video has come, from 0 to 100
        public int ProgressPercent { get; set; }

        // When a coach asked for the video to be made. The cutup that has waited longest is made first.
        public DateTime? QueuedAt { get; set; }

        // When the video was finished
        public DateTime? RenderedAt { get; set; }

        // Foreign key for the User entity (the coach who made the cutup)
        public int CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        //Navigation property to represent the One-to-Many relationship with CutupClip (the plays, in order)
        public List<CutupClip>? Clips { get; set; }

        //Navigation property to represent the One-to-Many relationship with FilmAssignment (who it is shared with)
        public List<FilmAssignment>? Assignments { get; set; }
    }
}
