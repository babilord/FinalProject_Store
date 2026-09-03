using FinalProject_Store.Application.Interfaces.Storage;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace FinalProject_Store.Infrastructures.Storage
{
    public sealed class MinioFileStorageService : IFileStorageService
    {
        private static readonly SemaphoreSlim BucketLock = new(1, 1);
        private readonly IMinioClient _client;
        private readonly MinioOptions _options;
        private volatile bool _bucketReady;

        public MinioFileStorageService(IMinioClient client, MinioOptions options)
        {
            _client = client;
            _options = options;
        }

        public async Task UploadAsync(string objectKey, Stream content, long contentLength,
            string contentType, CancellationToken cancellationToken = default)
        {
            ValidateProductObjectKey(objectKey);
            await EnsureBucketExistsAsync(cancellationToken);

            await _client.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectKey)
                .WithStreamData(content)
                .WithObjectSize(contentLength)
                .WithContentType(contentType), cancellationToken);
        }

        public async Task<StoredFileDto?> GetAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            ValidateProductObjectKey(objectKey);
            var content = new MemoryStream();
            try
            {
                await _client.GetObjectAsync(new GetObjectArgs()
                    .WithBucket(_options.BucketName)
                    .WithObject(objectKey)
                    .WithCallbackStream(async stream =>
                        await stream.CopyToAsync(content, cancellationToken)), cancellationToken);
                content.Position = 0;
                return new StoredFileDto
                {
                    Content = content,
                    ContentType = GetContentType(objectKey)
                };
            }
            catch (ObjectNotFoundException)
            {
                await content.DisposeAsync();
                return null;
            }
            catch (BucketNotFoundException)
            {
                await content.DisposeAsync();
                return null;
            }
            catch
            {
                await content.DisposeAsync();
                throw;
            }
        }

        public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            ValidateProductObjectKey(objectKey);
            await _client.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectKey), cancellationToken);
        }

        private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
        {
            if (_bucketReady) return;

            await BucketLock.WaitAsync(cancellationToken);
            try
            {
                if (_bucketReady) return;
                var exists = await _client.BucketExistsAsync(new BucketExistsArgs()
                    .WithBucket(_options.BucketName), cancellationToken);
                if (!exists)
                {
                    await _client.MakeBucketAsync(new MakeBucketArgs()
                        .WithBucket(_options.BucketName), cancellationToken);
                }
                _bucketReady = true;
            }
            finally
            {
                BucketLock.Release();
            }
        }

        private static void ValidateProductObjectKey(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey) || !objectKey.StartsWith("products/", StringComparison.Ordinal) ||
                objectKey.Contains("..", StringComparison.Ordinal) || objectKey.Contains('\\'))
                throw new ArgumentException("The object key is not a valid product image key.", nameof(objectKey));

            var fileName = objectKey["products/".Length..];
            var extension = Path.GetExtension(fileName);
            var identifier = Path.GetFileNameWithoutExtension(fileName);
            if (fileName.Contains('/') || identifier.Length != 32 || !Guid.TryParseExact(identifier, "N", out _) ||
                extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                throw new ArgumentException("The object key is not a valid product image key.", nameof(objectKey));
        }

        private static string GetContentType(string objectKey) => Path.GetExtension(objectKey) switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
