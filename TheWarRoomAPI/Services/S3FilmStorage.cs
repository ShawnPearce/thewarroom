using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace TheWarRoomAPI.Services
{
    // Film storage in an Amazon S3 bucket. The browser sends and plays files straight to and from the bucket
    // with signed links, so video never travels through the site or this API.
    //
    // The bucket needs a CORS rule that lets the site's address send files to it (see the README),
    // and the credentials this API runs with need to read, write and delete objects in it.
    public class S3FilmStorage : IFilmStorage
    {
        // A link handed to ffmpeg must outlast the longest job
        private static readonly TimeSpan ProcessingLinkLife = TimeSpan.FromHours(12);

        private readonly IAmazonS3? _client;
        private readonly string _bucket;

        public S3FilmStorage(IOptions<FilmOptions> options)
        {
            var settings = options.Value;
            _bucket = settings.S3Bucket?.Trim() ?? string.Empty;

            if (_bucket.Length == 0)
            {
                Problem = "Film storage is set to Amazon S3, but no bucket has been named (Film:S3Bucket).";
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.S3Region))
            {
                Problem = "Film storage is set to Amazon S3, but the bucket's region has not been set (Film:S3Region).";
                return;
            }

            try
            {
                var region = RegionEndpoint.GetBySystemName(settings.S3Region.Trim());

                if (string.IsNullOrWhiteSpace(settings.S3Profile))
                {
                    // The usual places: environment variables, the default profile, the server's role
                    _client = new AmazonS3Client(region);
                }
                else if (new CredentialProfileStoreChain().TryGetAWSCredentials(settings.S3Profile.Trim(), out AWSCredentials credentials))
                {
                    _client = new AmazonS3Client(credentials, region);
                }
                else
                {
                    Problem = $"The AWS profile \"{settings.S3Profile.Trim()}\" was not found on this server.";
                }
            }
            catch (AmazonClientException)
            {
                Problem = "No AWS credentials were found on this server for film storage.";
            }
        }

        // For a client that was built somewhere else
        public S3FilmStorage(IAmazonS3 client, string bucket)
        {
            _client = client;
            _bucket = bucket;
        }

        public string Description => $"the Amazon S3 bucket \"{_bucket}\"";

        public string? Problem { get; }

        public bool UsesSignedLinks => true;

        public string CreateUploadLink(string key, string contentType, TimeSpan validFor)
        {
            // The content type is part of what is signed, so the browser has to send exactly this one
            return Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _bucket,
                Key = key,
                Verb = HttpVerb.PUT,
                ContentType = contentType,
                Expires = DateTime.UtcNow.Add(validFor)   // the AWS SDK measures this against the real clock
            });
        }

        public string CreatePlaybackLink(string key, TimeSpan validFor)
        {
            return Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _bucket,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(validFor)
            });
        }

        public Task SaveAsync(string key, Stream content, long maxBytes, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Files go straight from the browser to the S3 bucket, not through this API.");
        }

        public string? FindFile(string key)
        {
            return null;   // nothing is kept on this computer
        }

        public async Task<long?> GetSizeAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                var facts = await Client.GetObjectMetadataAsync(_bucket, key, cancellationToken);
                return facts.ContentLength;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public string GetProcessingInput(string key)
        {
            // ffmpeg reads over HTTPS and asks only for the parts of the file it needs
            return CreatePlaybackLink(key, ProcessingLinkLife);
        }

        public async Task PutFileAsync(string key, string filePath, string contentType, CancellationToken cancellationToken)
        {
            await Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                FilePath = filePath,
                ContentType = contentType
            }, cancellationToken);
        }

        public async Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            await Client.DeleteObjectAsync(_bucket, key, cancellationToken);   // S3 does not mind when the object is not there
        }

        private IAmazonS3 Client => _client ?? throw new InvalidOperationException(Problem ?? "Film storage is not set up.");
    }
}
