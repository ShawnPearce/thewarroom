using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // The uploaded film of a game: the record, and the file behind it in film storage
    public interface IVideoService
    {
        // Whether this server can take video, and where it keeps it
        FilmSetupDto GetSetup(CurrentUser currentUser);

        // A video with its game and its clips in the order they happen
        Task<VideoDetailDto?> GetVideoAsync(CurrentUser currentUser, int id);

        // ----- Uploading: start, send the file, complete -----

        // Makes the record and says where the browser sends the file
        Task<UploadTicketDto?> StartUploadAsync(CurrentUser currentUser, StartUploadDto dto);

        // Takes the file itself. Only for film storage without signed links.
        Task<bool> ReceiveFileAsync(CurrentUser currentUser, int id, Stream content, CancellationToken cancellationToken);

        // Checks the file that arrived. The video is ready to be cut into plays afterwards, or it has failed.
        Task<VideoDto?> CompleteUploadAsync(CurrentUser currentUser, int id, CancellationToken cancellationToken);

        // ----- The file -----

        // How the browser gets the file to play it; null when there is nothing to play
        Task<FilmFile?> GetFileAsync(CurrentUser currentUser, int id);

        Task<bool> DeleteAsync(CurrentUser currentUser, int id, CancellationToken cancellationToken);
    }

    // How a video file reaches the browser: a signed link to go to, or the file on this computer to send
    public class FilmFile
    {
        public string? SignedLink { get; set; }
        public string? Path { get; set; }
        public string ContentType { get; set; } = "video/mp4";
    }
}
