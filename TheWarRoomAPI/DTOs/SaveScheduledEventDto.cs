using System.ComponentModel.DataAnnotations;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    public class SaveScheduledEventDto
    {
        public EventType EventType { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(100, ErrorMessage = "Title can't exceed 100 characters.")]
        public string Title { get; set; } = string.Empty;

        public DateOnly EventDate { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly? EndTime { get; set; }

        [Required(ErrorMessage = "Location is required.")]
        [StringLength(150, ErrorMessage = "Location can't exceed 150 characters.")]
        public string Location { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Notes can't exceed 500 characters.")]
        public string? Notes { get; set; }
    }
}
