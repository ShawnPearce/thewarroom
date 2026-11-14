using TheWarRoomApp.Models;

namespace TheWarRoomApp.Infrastructure
{
    // How enum values, ranks and dates are worded on the pages
    public static class DisplayText
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

        public static string Role(UserRole role, TeamUnit? coordinatorUnit)
        {
            return role switch
            {
                UserRole.HeadCoach => "Head coach",
                UserRole.Coordinator => coordinatorUnit != null ? $"{Unit(coordinatorUnit.Value)} coordinator" : "Coordinator",
                UserRole.PositionCoach => "Position coach",
                _ => "Player"
            };
        }

        public static string Status(RosterStatus status)
        {
            return status.ToString();
        }

        // 1 -> "1st", 2 -> "2nd", 3 -> "3rd", 4 -> "4th", 11 -> "11th"
        public static string Ordinal(int number)
        {
            int lastTwoDigits = number % 100;
            if (lastTwoDigits >= 11 && lastTwoDigits <= 13)
            {
                return $"{number}th";
            }

            return (number % 10) switch
            {
                1 => $"{number}st",
                2 => $"{number}nd",
                3 => $"{number}rd",
                _ => $"{number}th"
            };
        }

        public static string WeekOf(DepthChartWeekDto week)
        {
            string text = $"Week of {week.WeekStartDate:MMM d, yyyy}";
            return string.IsNullOrWhiteSpace(week.Label) ? text : $"{text} ({week.Label})";
        }

        // 71 -> 5'11"
        public static string Height(int? inches)
        {
            if (inches == null)
            {
                return string.Empty;
            }

            return $"{inches / 12}'{inches % 12}\"";
        }

        // "#7 Jalen Carter", or just the name when the player has no number yet
        public static string Player(int? jerseyNumber, string firstName, string lastName)
        {
            return jerseyNumber != null ? $"#{jerseyNumber} {firstName} {lastName}" : $"{firstName} {lastName}";
        }
    }
}
