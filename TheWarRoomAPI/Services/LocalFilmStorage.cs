using Microsoft.Extensions.Options;

namespace TheWarRoomAPI.Services
{
    // Film storage in a folder on this computer. Used in development, and wherever no S3 bucket is set up.
    // The bytes travel through the site and this API (see VideoController and CutupController).
    public class LocalFilmStorage : IFilmStorage
    {
        private const int BufferSize = 1024 * 1024;

        private readonly string _root;

        public LocalFilmStorage(IOptions<FilmOptions> options, IHostEnvironment environment)
        {
            string folder = string.IsNullOrWhiteSpace(options.Value.LocalFolder) ? "film-storage" : options.Value.LocalFolder.Trim();

            _root = Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(environment.ContentRootPath, folder));
        }

        public string Description => "a folder on this computer";

        public string? Problem => null;

        public bool UsesSignedLinks => false;

        public string CreateUploadLink(string key, string contentType, TimeSpan validFor)
        {
            throw new NotSupportedException("Local film storage has no signed links: the file is sent through the site.");
        }

        public string CreatePlaybackLink(string key, TimeSpan validFor)
        {
            throw new NotSupportedException("Local film storage has no signed links: the file is played through the site.");
        }

        public async Task SaveAsync(string key, Stream content, long maxBytes, CancellationToken cancellationToken)
        {
            string path = PathOf(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // The file is written under another name first, so a half-sent file is never taken for a whole one
            string unfinished = path + ".part";
            try
            {
                await using (var file = new FileStream(unfinished, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                {
                    var buffer = new byte[BufferSize];
                    long written = 0;
                    int read;

                    while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        written += read;
                        if (written > maxBytes)
                        {
                            throw new InvalidOperationException("The file is larger than film storage accepts.");
                        }

                        await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                }

                File.Move(unfinished, path, overwrite: true);
            }
            finally
            {
                File.Delete(unfinished);   // does nothing when the file was moved to its real name
            }
        }

        public string? FindFile(string key)
        {
            string path = PathOf(key);
            return File.Exists(path) ? path : null;
        }

        public Task<long?> GetSizeAsync(string key, CancellationToken cancellationToken)
        {
            var file = new FileInfo(PathOf(key));
            return Task.FromResult(file.Exists ? file.Length : (long?)null);
        }

        public string GetProcessingInput(string key)
        {
            return PathOf(key);
        }

        public Task PutFileAsync(string key, string filePath, string contentType, CancellationToken cancellationToken)
        {
            string path = PathOf(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(filePath, path, overwrite: true);

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            File.Delete(PathOf(key));   // not an error when the file is not there
            return Task.CompletedTask;
        }

        // Keys are made up by this API, never taken from a request.
        // This check still makes sure that no key can ever point outside the storage folder.
        private string PathOf(string key)
        {
            string path = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));

            if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new ArgumentException("That is not a file in film storage.", nameof(key));
            }

            return path;
        }
    }
}
