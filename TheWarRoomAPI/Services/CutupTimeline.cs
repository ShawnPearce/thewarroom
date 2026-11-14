namespace TheWarRoomAPI.Services
{
    // A cutup plays its plays one after another in their order, with the time between plays removed.
    // So every play starts where the one before it ends.
    public static class CutupTimeline
    {
        // Given the length of each play in order, returns where each one starts in the cutup (all in milliseconds)
        public static List<int> Offsets(IEnumerable<int> durationsMs)
        {
            var offsets = new List<int>();
            int elapsed = 0;

            foreach (int duration in durationsMs)
            {
                offsets.Add(elapsed);
                elapsed += duration;
            }

            return offsets;
        }
    }
}
