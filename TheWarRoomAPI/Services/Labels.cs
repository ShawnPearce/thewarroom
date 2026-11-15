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

        // "American football" or "Canadian football"
        public static string Rules(FootballRules rules)
        {
            return rules == FootballRules.Canadian ? "Canadian football" : "American football";
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

        // The size of a file: 5368709120 -> "5 GB", 367001600 -> "350 MB"
        public static string Size(long bytes)
        {
            const double megabyte = 1024 * 1024;
            const double gigabyte = megabyte * 1024;

            return bytes >= gigabyte
                ? (bytes / gigabyte).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " GB"
                : Math.Max(1, Math.Round(bytes / megabyte)).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
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
