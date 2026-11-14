using System.Globalization;

namespace TheWarRoomApp.Infrastructure
{
    // Times in a video the way people write them: "12:34", "1:02:03", or "12:34.5" with tenths of a second.
    // The API keeps them as whole milliseconds from the start of the video.
    public static class TimeText
    {
        // 754000 -> "12:34", 4530500 -> "1:15:30.5"
        public static string Format(int milliseconds)
        {
            var time = TimeSpan.FromMilliseconds(milliseconds);

            string text = time.TotalHours >= 1
                ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
                : $"{time.Minutes}:{time.Seconds:00}";

            int tenths = time.Milliseconds / 100;
            return tenths > 0 ? $"{text}.{tenths}" : text;
        }

        // How long something lasts, in words: 8000 -> "8 s", 9500 -> "9.5 s", 125000 -> "2 min 5 s", 3900000 -> "1 h 5 min"
        public static string Length(int milliseconds)
        {
            // A single play is short, so its tenths of a second are worth showing
            if (milliseconds < 60_000)
            {
                return (milliseconds / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + " s";
            }

            var time = TimeSpan.FromSeconds(Math.Round(milliseconds / 1000.0));

            if (time.TotalHours >= 1)
            {
                return $"{(int)time.TotalHours} h {time.Minutes} min";
            }
            return $"{time.Minutes} min {time.Seconds} s";
        }

        // Reads "12:34", "1:02:03", "12:34.5" or plain seconds such as "75". False when the text is not a time.
        public static bool TryParse(string? text, out int milliseconds)
        {
            milliseconds = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            // The last part is the seconds; the parts before it are the minutes and then the hours
            string[] parts = text.Trim().Split(':');
            if (parts.Length > 3)
            {
                return false;
            }

            if (!decimal.TryParse(parts[^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal seconds))
            {
                return false;
            }

            int minutes = 0;
            int hours = 0;
            if (parts.Length >= 2 && !int.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture, out minutes))
            {
                return false;
            }
            if (parts.Length == 3 && !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hours))
            {
                return false;
            }

            // "12:75" is not a time: seconds and minutes stop at 59 when there is a larger part in front of them
            if ((parts.Length >= 2 && seconds >= 60) || (parts.Length == 3 && minutes >= 60))
            {
                return false;
            }

            decimal total = (hours * 3600m + minutes * 60m + seconds) * 1000m;
            if (total > int.MaxValue)
            {
                return false;
            }

            milliseconds = (int)Math.Round(total);
            return true;
        }
    }
}
