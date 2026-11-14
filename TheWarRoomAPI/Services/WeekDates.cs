namespace TheWarRoomAPI.Services
{
    // Depth charts are kept per week, and a week is identified by its Monday
    public static class WeekDates
    {
        public static DateOnly MondayOf(DateOnly date)
        {
            // DayOfWeek counts Sunday as 0, so Monday becomes 0 and Sunday becomes 6
            int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-daysSinceMonday);
        }

        public static DateOnly Today(TimeProvider clock)
        {
            return DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        }
    }
}
