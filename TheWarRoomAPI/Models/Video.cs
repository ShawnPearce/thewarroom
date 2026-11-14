using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.Models
{
    // The film of a game as it was uploaded. The file itself is kept in film storage;
    // this record holds where it is and what is known about it.
    public class Video
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Foreign key for the Game entity
        public int GameId { get; set; }
        public Game? Game { get; set; }

        // For example "Full game" or "End zone angle"
        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        public VideoStatus Status { get; set; }

        // Where the file is in film storage; empty when there is no file
        [StringLength(300)]
        public string? StorageKey { get; set; }

        [StringLength(260)]
        public string? OriginalFileName { get; set; }

        [StringLength(100)]
        public string? ContentType { get; set; }

        public long? SizeBytes { get; set; }

        // The length of the video in milliseconds, known once the upload has been checked
        public int? DurationMs { get; set; }

        // Why the upload could not be used, when Status is Failed
        [StringLength(300)]
        public string? FailureReason { get; set; }

        // Foreign key for the User entity (the coach who uploaded it)
        public int UploadedById { get; set; }
        public User? UploadedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        //Navigation property to represent the One-to-Many relationship with Clip
        public List<Clip>? Clips { get; set; }
    }
}
