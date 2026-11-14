using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.Models
{
    // One play cut out of a video: the part between StartMs and EndMs.
    // Times are whole milliseconds from the start of the video.
    public class Clip
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Foreign key for the Video entity
        public int VideoId { get; set; }
        public Video? Video { get; set; }

        public int StartMs { get; set; }
        public int EndMs { get; set; }

        // A short note about the play, for example "Touchdown" or "Missed assignment"
        [StringLength(100)]
        public string? Title { get; set; }

        // The arrows and text drawn over the clip, as JSON
        public string? AnnotationsJson { get; set; }

        // Foreign key for the User entity (the coach who cut the clip)
        public int CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        //Navigation property to represent the One-to-One relationship with PlayTag (a clip may have no tag)
        public PlayTag? Tag { get; set; }

        //Navigation property for the cutups this clip has been put in
        public List<CutupClip>? CutupClips { get; set; }
    }
}
