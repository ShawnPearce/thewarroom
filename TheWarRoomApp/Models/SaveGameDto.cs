using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    public class SaveGameDto
    {
        [Display(Name = "Date")]
        public DateOnly GameDate { get; set; }

        // Both are ids of teams on film (ScoutedTeam)
        [Range(1, int.MaxValue, ErrorMessage = "Choose the home team.")]
        [Display(Name = "Home team")]
        public int HomeTeamId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Choose the away team.")]
        [Display(Name = "Away team")]
        public int AwayTeamId { get; set; }

        [StringLength(60, ErrorMessage = "The label can't exceed 60 characters.")]
        public string? Label { get; set; }

        [StringLength(500, ErrorMessage = "Notes can't exceed 500 characters.")]
        public string? Notes { get; set; }
    }
}
