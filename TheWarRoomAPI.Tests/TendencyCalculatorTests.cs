using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The arithmetic of a run/pass tendency, and the distance ranges (no database needed)
    public class TendencyCalculatorTests
    {
        [Fact]
        public void TwelveRunsAmongTwentyPlays_IsASixtyPercentRunTendency()
        {
            var split = TendencyCalculator.Split(runs: 12, passes: 8);

            Assert.Equal(60.0m, split.RunPercent);
            Assert.Equal(40.0m, split.PassPercent);
            Assert.Equal(20, split.Plays);   // the sample size goes with the percentages
        }

        [Fact]
        public void NoMatchingPlays_HasNoPercentages()
        {
            var split = TendencyCalculator.Split(runs: 0, passes: 0);

            Assert.Equal(0, split.Plays);
            Assert.Null(split.RunPercent);   // not 0%: there is simply no data
            Assert.Null(split.PassPercent);
        }

        [Theory]
        [InlineData(1, 2, 33.3, 66.7)]     // a third does not come out even: one decimal
        [InlineData(2, 1, 66.7, 33.3)]
        [InlineData(1, 15, 6.3, 93.7)]     // 6.25 rounds up to 6.3
        [InlineData(7, 0, 100.0, 0.0)]     // every play a run
        [InlineData(0, 5, 0.0, 100.0)]     // every play a pass
        [InlineData(1, 1, 50.0, 50.0)]
        public void Percentages_HaveOneDecimal_AndAlwaysAddUpToAHundred(int runs, int passes, double runPercent, double passPercent)
        {
            var split = TendencyCalculator.Split(runs, passes);

            Assert.Equal((decimal)runPercent, split.RunPercent);
            Assert.Equal((decimal)passPercent, split.PassPercent);
            Assert.Equal(100m, split.RunPercent + split.PassPercent);
        }

        [Theory]
        [InlineData(1, DistanceRange.Short)]
        [InlineData(3, DistanceRange.Short)]
        [InlineData(4, DistanceRange.Medium)]
        [InlineData(6, DistanceRange.Medium)]
        [InlineData(7, DistanceRange.Long)]
        [InlineData(10, DistanceRange.Long)]
        [InlineData(11, DistanceRange.VeryLong)]
        [InlineData(25, DistanceRange.VeryLong)]
        public void ADistance_FallsInItsRange(int yardsToGo, DistanceRange expected)
        {
            Assert.Equal(expected, TendencyCalculator.RangeOf(yardsToGo));
        }

        [Fact]
        public void TheRanges_CoverEveryDistance_WithoutOverlapping()
        {
            // Every distance from 1 to 99 yards lies inside the bounds of the range it is put in, and of no other
            for (int yards = 1; yards <= 99; yards++)
            {
                var containing = TendencyCalculator.Ranges
                    .Where(range =>
                    {
                        var (first, last) = TendencyCalculator.Bounds(range);
                        return yards >= first && (last == null || yards <= last);
                    })
                    .ToList();

                Assert.Equal(new[] { TendencyCalculator.RangeOf(yards) }, containing);
            }
        }
    }
}
