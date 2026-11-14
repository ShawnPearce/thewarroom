using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    public class SaveScheduledEventDto
    {
        [Display(Name = "Type")]
        public EventType EventType { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(100, ErrorMessage = "Title can't exceed 100 characters.")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Date")]
        public DateOnly EventDate { get; set; }

        [Display(Name = "Starts")]
        public TimeOnly StartTime { get; set; }

        [Display(Name = "Ends")]
        public TimeOnly? EndTime { get; set; }

        [Required(ErrorMessage = "Location is required.")]
        [StringLength(150, ErrorMessage = "Location can't exceed 150 characters.")]
        public string Location { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Notes can't exceed 500 characters.")]
        public string? Notes { get; set; }
    }
}
