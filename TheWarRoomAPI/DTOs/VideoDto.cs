using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    public class VideoDto
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public string Title { get; set; } = string.Empty;
        public VideoStatus Status { get; set; }

        // The length of the video in milliseconds; empty until the upload has been checked
        public int? DurationMs { get; set; }

        // True when the video is ready and has a file in film storage, so it can be played
        public bool HasFile { get; set; }

        // The name the file had on the coach's computer, and its size
        public string? FileName { get; set; }
        public long? SizeBytes { get; set; }

        // Why the upload could not be used, when Status is Failed
        public string? FailureReason { get; set; }

        public int ClipCount { get; set; }
        public int TaggedClipCount { get; set; }

        public string UploadedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    // A video with the game it belongs to and its clips in the order they happen
    public class VideoDetailDto : VideoDto
    {
        public DateOnly GameDate { get; set; }
        public string? GameLabel { get; set; }

        // The two teams of the game: one of them is on offence in every tagged play
        public int HomeTeamId { get; set; }
        public string HomeTeamName { get; set; } = string.Empty;
        public int AwayTeamId { get; set; }
        public string AwayTeamName { get; set; } = string.Empty;

        // How many downs a play can be tagged with: four in American football, three in Canadian football
        public int Downs { get; set; }

        public List<ClipDto> Clips { get; set; } = new List<ClipDto>();
    }
}
