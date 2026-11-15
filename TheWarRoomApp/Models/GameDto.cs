using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // A game on film, as a row in the list
    public class GameDto
    {
        public int Id { get; set; }
        [Display(Name = "Date")]
        public DateOnly GameDate { get; set; }

        public int HomeTeamId { get; set; }
        public string HomeTeamName { get; set; } = string.Empty;
        public int AwayTeamId { get; set; }
        public string AwayTeamName { get; set; } = string.Empty;

        public string? Label { get; set; }
        public string? Notes { get; set; }

        public int VideoCount { get; set; }
        public int ClipCount { get; set; }

        // How many of the clips are tagged, and so count in the tendency charts
        public int TaggedClipCount { get; set; }

        // How many downs the team's game has: four in American football, three in Canadian football
        public int Downs { get; set; } = 4;
    }

    // A game with its videos
    public class GameDetailDto : GameDto
    {
        public List<VideoDto> Videos { get; set; } = new List<VideoDto>();
    }
}
