using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // The arithmetic behind every tendency figure. It needs no database, so it is tested on its own.
    public static class TendencyCalculator
    {
        // The distance ranges from the shortest to the longest
        public static readonly DistanceRange[] Ranges =
        {
            DistanceRange.Short, DistanceRange.Medium, DistanceRange.Long, DistanceRange.VeryLong
        };

        // Business rule: of the plays tagged as a run or a pass,
        //   run %  = runs ÷ (runs + passes), to one decimal
        //   pass % = the rest, so the two always add up to 100
        // and the sample size (runs + passes) always goes with them.
        // With no matching plays there is no percentage at all, which the pages show as "No data".
        // Example: 12 runs among 20 plays is a 60.0% run tendency.
        public static TendencySplitDto Split(int runs, int passes, int? down = null, DistanceRange? distance = null)
        {
            int plays = runs + passes;

            decimal? runPercent = null;
            decimal? passPercent = null;
            if (plays > 0)
            {
                runPercent = Math.Round(runs * 100m / plays, 1, MidpointRounding.AwayFromZero);
                passPercent = 100m - runPercent;
            }

            return new TendencySplitDto
            {
                Down = down,
                Distance = distance,
                Runs = runs,
                Passes = passes,
                Plays = plays,
                RunPercent = runPercent,
                PassPercent = passPercent
            };
        }

        // Which range a distance to go falls in
        public static DistanceRange RangeOf(int distance)
        {
            if (distance <= 3)
            {
                return DistanceRange.Short;
            }
            if (distance <= 6)
            {
                return DistanceRange.Medium;
            }
            if (distance <= 10)
            {
                return DistanceRange.Long;
            }
            return DistanceRange.VeryLong;
        }

        // The first and the last yard of a range. Very long has no last yard.
        public static (int Min, int? Max) Bounds(DistanceRange range)
        {
            return range switch
            {
                DistanceRange.Short => (1, 3),
                DistanceRange.Medium => (4, 6),
                DistanceRange.Long => (7, 10),
                _ => (11, null)
            };
        }
    }
}
