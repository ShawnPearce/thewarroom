namespace TheWarRoomAPI.Services
{
    // Where video files are kept. The database holds only each file's key: a name like
    // "teams/3/videos/9f3a...c1.mp4" that this API makes up and that never comes from a request.
    //
    // There are two kinds of storage, and they move the bytes differently:
    //   signed links (Amazon S3): the browser sends and plays the file straight to and from storage,
    //                             using a link that is signed here and stops working after a while
    //   no signed links (a folder on this computer): the bytes travel through the site and this API
    public interface IFilmStorage
    {
        // For the pages, for example "a folder on this computer"
        string Description { get; }

        // Empty when files can be stored; otherwise the reason they cannot, in words
        string? Problem { get; }

        bool UsesSignedLinks { get; }

        // ----- With signed links -----

        // A link the browser can send (PUT) one file to. The browser must send the same content type.
        string CreateUploadLink(string key, string contentType, TimeSpan validFor);

        // A link the browser can play the file from
        string CreatePlaybackLink(string key, TimeSpan validFor);

        // ----- Without signed links -----

        // Writes what was sent to storage. Throws InvalidOperationException when it is larger than maxBytes.
        Task SaveAsync(string key, Stream content, long maxBytes, CancellationToken cancellationToken);

        // The file's full path on this computer, or null when there is no such file
        string? FindFile(string key);

        // ----- Both -----

        // The size of the file in bytes, or null when there is no such file
        Task<long?> GetSizeAsync(string key, CancellationToken cancellationToken);

        // Something ffmpeg and ffprobe can open: a path on this computer, or a signed link
        string GetProcessingInput(string key);

        // Puts a file that was made on this computer (a finished cutup) into storage
        Task PutFileAsync(string key, string filePath, string contentType, CancellationToken cancellationToken);

        // Deletes the file. Deleting a file that is not there is not an error.
        Task DeleteAsync(string key, CancellationToken cancellationToken);
    }
}
