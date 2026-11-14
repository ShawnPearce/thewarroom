namespace TheWarRoomApp.Models
{
    // What the study timer on a film or playbook page counts time for
    public class StudyTimerViewModel
    {
        public ViewingKind Kind { get; set; }

        // The playbook being read, or the cutup being watched
        public int? PlaybookId { get; set; }
        public int? CutupId { get; set; }

        // For film: the CSS selector of the video on the page. Time counts only while it is playing.
        public string? VideoSelector { get; set; }
    }
}
