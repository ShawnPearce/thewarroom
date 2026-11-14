using System.ComponentModel.DataAnnotations;


namespace TheWarRoomApp.Models
{
    // One player on the roster
    public class RosterEntryDto
    {
        public int PlayerId { get; set; }
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        // Left empty when a player is looking at the roster
        public string? Email { get; set; }

        [Display(Name = "Jersey number")]
        public int? JerseyNumber { get; set; }
        public int? Grade { get; set; }
        [Display(Name = "Height (inches)")]
        public int? HeightInches { get; set; }
        [Display(Name = "Weight (lbs)")]
        public int? WeightLbs { get; set; }
        public RosterStatus Status { get; set; }
        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        // The position groups the player belongs to
        public List<GroupSummaryDto> Groups { get; set; } = new List<GroupSummaryDto>();
    }
}
