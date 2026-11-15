using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Film storage kept in memory, so the tests can see what was stored under which key.
    // It can behave as either kind of storage: with signed links (like Amazon S3) or without (like the local folder).
    public class FakeFilmStorage : IFilmStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>();

        public bool SignedLinks { get; set; }

        public string Description => "test storage";

        public string? Problem { get; set; }

        public bool UsesSignedLinks => SignedLinks;

        public string CreateUploadLink(string key, string contentType, TimeSpan validFor)
        {
            return $"https://storage.test/{key}?upload";
        }

        public string CreatePlaybackLink(string key, TimeSpan validFor)
        {
            return $"https://storage.test/{key}?play";
        }

        public async Task SaveAsync(string key, Stream content, long maxBytes, CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken);

            if (memory.Length > maxBytes)
            {
                throw new InvalidOperationException("The file is larger than film storage accepts.");
            }

            Files[key] = memory.ToArray();
        }

        public string? FindFile(string key)
        {
            return Files.ContainsKey(key) ? "/test-storage/" + key : null;
        }

        public Task<long?> GetSizeAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(Files.TryGetValue(key, out var bytes) ? bytes.LongLength : (long?)null);
        }

        public string GetProcessingInput(string key)
        {
            return "storage:" + key;
        }

        public Task PutFileAsync(string key, string filePath, string contentType, CancellationToken cancellationToken)
        {
            Files[key] = File.ReadAllBytes(filePath);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            Files.Remove(key);
            return Task.CompletedTask;
        }

        // What a browser does with a signed link: the file arrives in storage without passing through the API
        public void BrowserSends(string key, int sizeBytes)
        {
            Files[key] = new byte[sizeBytes];
        }
    }
}
