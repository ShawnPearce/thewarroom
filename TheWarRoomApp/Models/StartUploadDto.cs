using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // What the browser knows about a video before it is sent
    public class StartUploadDto
    {
        // The game the video is film of
        public int GameId { get; set; }

        // For example "Full game" or "End zone angle"
        [Required(ErrorMessage = "Give the video a title.")]
        [StringLength(100, ErrorMessage = "The title can't exceed 100 characters.")]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        // The name of the file on the coach's computer. Its ending decides whether the file is taken.
        [Required(ErrorMessage = "Choose a video file.")]
        [StringLength(260, ErrorMessage = "The file name can't exceed 260 characters.")]
        public string FileName { get; set; } = string.Empty;

        [Range(1, long.MaxValue, ErrorMessage = "The file is empty.")]
        public long SizeBytes { get; set; }
    }

    // The answer to a started upload: the record that was made and where the browser sends the file
    public class UploadTicketDto
    {
        public int VideoId { get; set; }

        // A signed link the browser sends the file to with PUT.
        // Empty when film storage has no signed links: the file is then sent through the site.
        public string? UploadUrl { get; set; }

        // The Content-Type the browser must send with the file
        public string ContentType { get; set; } = string.Empty;
    }
}
