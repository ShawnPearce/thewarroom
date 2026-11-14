using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // One play in a cutup, at its place in the order.
    // The times, title and drawings are copied from the clip, so that once the cutup has been made
    // into a video, later changes to the clip never change film that was already shared.
    [Index(nameof(CutupId), nameof(ClipId), IsUnique = true)]
    public class CutupClip
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Foreign key for the Cutup entity
        public int CutupId { get; set; }
        public Cutup? Cutup { get; set; }

        // 1 is played first
        public int Position { get; set; }

        // Foreign key for the Clip entity (the clip this play was taken from)
        public int ClipId { get; set; }
        public Clip? Clip { get; set; }

        // ----- The copy of the clip -----

        // Foreign key for the Video entity (the video the play is cut from)
        public int VideoId { get; set; }
        public Video? Video { get; set; }

        public int StartMs { get; set; }
        public int EndMs { get; set; }

        [StringLength(100)]
        public string? Title { get; set; }

        public string? AnnotationsJson { get; set; }
    }
}
