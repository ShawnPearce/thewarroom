using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.DTOs
{
    public class SaveGameDto
    {
        public DateOnly GameDate { get; set; }

        // Both are ids of teams on film (ScoutedTeam)
        [Range(1, int.MaxValue, ErrorMessage = "Choose the home team.")]
        public int HomeTeamId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Choose the away team.")]
        public int AwayTeamId { get; set; }

        [StringLength(60, ErrorMessage = "The label can't exceed 60 characters.")]
        public string? Label { get; set; }

        [StringLength(500, ErrorMessage = "Notes can't exceed 500 characters.")]
        public string? Notes { get; set; }
    }
}
