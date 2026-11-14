namespace TheWarRoomAPI.Tests
{
    // A clock the tests control, so time-based rules can be checked without waiting
    public class TestClock : TimeProvider
    {
        private DateTimeOffset _now;

        public TestClock(DateTimeOffset start)
        {
            _now = start;
        }

        // Tests always run in UTC, so "today" does not depend on the machine's time zone
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }

        public void Advance(TimeSpan time)
        {
            _now = _now.Add(time);
        }
    }
}
