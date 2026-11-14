using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Where each play starts when a cutup's plays are played one after another (no database needed)
    public class CutupTimelineTests
    {
        [Fact]
        public void EachPlay_StartsWhereTheOneBeforeItEnds()
        {
            // Three plays of 8, 6.5 and 12 seconds
            var offsets = CutupTimeline.Offsets(new[] { 8000, 6500, 12000 });

            Assert.Equal(new[] { 0, 8000, 14500 }, offsets);
        }

        [Fact]
        public void ACutupWithNoPlays_HasNoOffsets()
        {
            Assert.Empty(CutupTimeline.Offsets(Array.Empty<int>()));
        }
    }
}
