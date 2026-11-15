namespace TheWarRoomApp.Models
{
    // The body sent to POST api/Team/register
    public class RegisterTeamDto
    {
        public string TeamName { get; set; } = string.Empty;
        public FootballRules Rules { get; set; } = FootballRules.American;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
