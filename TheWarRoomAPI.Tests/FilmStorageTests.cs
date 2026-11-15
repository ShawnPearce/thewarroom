using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The two places video files can be kept: a folder on this computer, and an Amazon S3 bucket
    public class FilmStorageTests : IDisposable
    {
        // A folder of its own for every test, removed again afterwards
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "warroom-tests", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }

        private LocalFilmStorage Local()
        {
            var options = Options.Create(new FilmOptions { LocalFolder = Path.Combine(_folder, "film") });
            return new LocalFilmStorage(options, new TestEnvironment(_folder));
        }

        private static MemoryStream FileOf(int sizeBytes)
        {
            return new MemoryStream(new byte[sizeBytes]);
        }

        // ---------- A folder on this computer ----------

        [Fact]
        public async Task TheLocalFolder_KeepsFindsAndDeletesFiles()
        {
            var storage = Local();
            const string key = "teams/3/videos/abc.mp4";

            Assert.False(storage.UsesSignedLinks);
            Assert.Null(storage.Problem);
            Assert.Null(await storage.GetSizeAsync(key, CancellationToken.None));
            Assert.Null(storage.FindFile(key));

            await storage.SaveAsync(key, FileOf(2500), maxBytes: 10_000, CancellationToken.None);

            Assert.Equal(2500, await storage.GetSizeAsync(key, CancellationToken.None));
            string path = storage.FindFile(key)!;
            Assert.Equal(Path.Combine(_folder, "film", "teams", "3", "videos", "abc.mp4"), path);
            Assert.Equal(path, storage.GetProcessingInput(key));

            await storage.DeleteAsync(key, CancellationToken.None);
            Assert.Null(storage.FindFile(key));

            await storage.DeleteAsync(key, CancellationToken.None);   // deleting what is not there is not an error
        }

        [Fact]
        public async Task AFileOverTheLimit_LeavesNothingBehind()
        {
            var storage = Local();
            const string key = "teams/3/videos/big.mp4";

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storage.SaveAsync(key, FileOf(10_001), maxBytes: 10_000, CancellationToken.None));

            Assert.Null(storage.FindFile(key));
            Assert.Empty(Directory.GetFiles(Path.Combine(_folder, "film", "teams", "3", "videos")));   // no half-written file either
        }

        [Fact]
        public async Task AFinishedCutup_IsCopiedIntoTheLocalFolder()
        {
            var storage = Local();
            Directory.CreateDirectory(_folder);
            string made = Path.Combine(_folder, "made.mp4");
            await File.WriteAllBytesAsync(made, new byte[777]);

            await storage.PutFileAsync("teams/3/cutups/xyz.mp4", made, "video/mp4", CancellationToken.None);

            Assert.Equal(777, await storage.GetSizeAsync("teams/3/cutups/xyz.mp4", CancellationToken.None));
            Assert.True(File.Exists(made));   // the copy that was made on this computer is the caller's to clean up
        }

        [Theory]
        [InlineData("../outside.mp4")]
        [InlineData("teams/../../outside.mp4")]
        [InlineData("/etc/passwd")]
        public async Task AKey_CanNeverPointOutsideTheStorageFolder(string key)
        {
            var storage = Local();

            Assert.Throws<ArgumentException>(() => storage.FindFile(key));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(key, FileOf(10), 10_000, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.DeleteAsync(key, CancellationToken.None));
        }

        // ---------- Amazon S3 ----------
        // Signing a link needs no network, so these run anywhere. Sending to a real bucket is not tested here.

        private static S3FilmStorage Bucket()
        {
            var client = new AmazonS3Client(new BasicAWSCredentials("test-key-id", "test-secret"), RegionEndpoint.CACentral1);
            return new S3FilmStorage(client, "warroom-film-test");
        }

        [Fact]
        public void S3_SignsALinkTheBrowserSendsTheFileTo()
        {
            var storage = Bucket();
            Assert.True(storage.UsesSignedLinks);

            var link = new Uri(storage.CreateUploadLink("teams/3/videos/abc.mp4", "video/mp4", TimeSpan.FromHours(1)));

            Assert.Equal("https", link.Scheme);
            Assert.Contains("warroom-film-test", link.Host);
            Assert.Contains("ca-central-1", link.Host);
            Assert.Equal("/teams/3/videos/abc.mp4", link.AbsolutePath);
            Assert.Contains("X-Amz-Signature=", link.Query);
            Assert.Contains("X-Amz-Expires=3600", link.Query);
            Assert.Contains("content-type", link.Query);   // the content type is part of what is signed
            Assert.DoesNotContain("test-secret", link.ToString());
        }

        [Fact]
        public void S3_SignsALinkTheVideoIsPlayedFrom()
        {
            var storage = Bucket();

            var link = new Uri(storage.CreatePlaybackLink("teams/3/videos/abc.mp4", TimeSpan.FromHours(6)));

            Assert.Equal("/teams/3/videos/abc.mp4", link.AbsolutePath);
            Assert.Contains("X-Amz-Expires=21600", link.Query);
            Assert.Null(storage.FindFile("teams/3/videos/abc.mp4"));   // nothing is kept on this computer
            Assert.StartsWith("https://", storage.GetProcessingInput("teams/3/videos/abc.mp4"));
        }

        [Fact]
        public void S3_WithoutABucketOrRegion_SaysWhatIsMissing()
        {
            var noBucket = new S3FilmStorage(Options.Create(new FilmOptions { Storage = "S3", S3Region = "ca-central-1" }));
            Assert.Contains("no bucket", noBucket.Problem);

            var noRegion = new S3FilmStorage(Options.Create(new FilmOptions { Storage = "S3", S3Bucket = "warroom-film-test" }));
            Assert.Contains("region", noRegion.Problem);

            var noProfile = new S3FilmStorage(Options.Create(new FilmOptions
            {
                Storage = "S3",
                S3Bucket = "warroom-film-test",
                S3Region = "ca-central-1",
                S3Profile = "a-profile-that-does-not-exist-" + Guid.NewGuid().ToString("N")
            }));
            Assert.Contains("was not found", noProfile.Problem);

            // Without a client nothing can be signed, and the reason is the same one the pages show
            var error = Assert.Throws<InvalidOperationException>(() => noBucket.CreatePlaybackLink("teams/3/videos/abc.mp4", TimeSpan.FromHours(1)));
            Assert.Equal(noBucket.Problem, error.Message);
        }

        // The least a storage needs to know about where the API runs
        private class TestEnvironment : IHostEnvironment
        {
            public TestEnvironment(string contentRoot)
            {
                ContentRootPath = contentRoot;
            }

            public string EnvironmentName { get; set; } = "Test";
            public string ApplicationName { get; set; } = "TheWarRoomAPI.Tests";
            public string ContentRootPath { get; set; }
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
