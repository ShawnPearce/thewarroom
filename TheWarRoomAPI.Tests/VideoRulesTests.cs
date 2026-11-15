using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // What an uploaded file has to be before it can be cut into plays
    public class VideoRulesTests
    {
        private static VideoFacts Facts(string container = FakeVideoTools.Mp4Family, string? codec = "h264", int durationMs = 60_000)
        {
            return new VideoFacts { Container = container, VideoCodec = codec, Width = 1920, Height = 1080, DurationMs = durationMs };
        }

        [Theory]
        [InlineData("week3.mp4", "video/mp4")]
        [InlineData("WEEK3.MP4", "video/mp4")]
        [InlineData("end zone.m4v", "video/mp4")]
        [InlineData("phone.MOV", "video/quicktime")]
        public void Mp4AndMovFiles_AreTaken(string fileName, string contentType)
        {
            Assert.Equal(contentType, VideoRules.ContentTypeOf(fileName));
        }

        [Theory]
        [InlineData("highlights.avi")]
        [InlineData("highlights.mkv")]
        [InlineData("highlights.webm")]
        [InlineData("week3.mp4.exe")]
        [InlineData("week3")]
        [InlineData("")]
        public void OtherFiles_AreNotTaken(string fileName)
        {
            Assert.Null(VideoRules.ContentTypeOf(fileName));
        }

        [Fact]
        public void AnH264VideoInAnMp4OrMovFile_CanBeUsed()
        {
            Assert.Null(VideoRules.WhyNotUsable(Facts()));
            Assert.Null(VideoRules.WhyNotUsable(Facts(durationMs: VideoRules.MinDurationMs)));
            Assert.Null(VideoRules.WhyNotUsable(Facts(durationMs: VideoRules.MaxDurationMs)));
        }

        [Fact]
        public void AFileWithoutAPicture_IsNotAVideo()
        {
            // A sound file renamed to .mp4, for example
            string? reason = VideoRules.WhyNotUsable(Facts(codec: null));

            Assert.Contains("no picture", reason);
        }

        [Fact]
        public void AnotherKindOfFileRenamedToMp4_IsRefused()
        {
            string? reason = VideoRules.WhyNotUsable(Facts(container: "matroska,webm"));

            Assert.Contains("MP4 and MOV", reason);
        }

        [Theory]
        [InlineData("hevc", "HEVC")]
        [InlineData("prores", "PRORES")]
        [InlineData("vp9", "VP9")]
        public void VideoThatIsNotH264_IsRefused_AndTheReasonNamesIt(string codec, string named)
        {
            string? reason = VideoRules.WhyNotUsable(Facts(codec: codec));

            Assert.Contains(named, reason);
            Assert.Contains("H.264", reason);
        }

        [Fact]
        public void AVideoHasToBeBetweenASecondAndSixHoursLong()
        {
            Assert.Contains("less than a second", VideoRules.WhyNotUsable(Facts(durationMs: VideoRules.MinDurationMs - 1)));
            Assert.Contains("six hours", VideoRules.WhyNotUsable(Facts(durationMs: VideoRules.MaxDurationMs + 1)));
        }
    }
}
