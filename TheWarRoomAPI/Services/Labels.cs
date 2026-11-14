using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // Wording used in messages and titles
    public static class Labels
    {
        public static string Unit(TeamUnit unit)
        {
            return unit switch
            {
                TeamUnit.Offence => "Offence",
                TeamUnit.Defence => "Defence",
                TeamUnit.SpecialTeams => "Special teams",
                _ => unit.ToString()
            };
        }

        public static string FullName(User user)
        {
            return $"{user.FirstName} {user.LastName}";
        }

        // "Riverside Raiders at Lakeshore Lions" (the game must be loaded with its two teams)
        public static string Matchup(Game game)
        {
            return $"{game.AwayTeam!.Name} at {game.HomeTeam!.Name}";
        }

        // A time in a video: 754000 -> "12:34", 4530000 -> "1:15:30"
        public static string Time(int milliseconds)
        {
            var time = TimeSpan.FromMilliseconds(milliseconds);
            return time.TotalHours >= 1
                ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
                : $"{time.Minutes}:{time.Seconds:00}";
        }
    }
}
