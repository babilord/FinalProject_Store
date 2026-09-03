namespace FinalProject_Store.Application.Interfaces.Storage
{
    public interface IFileStorageService
    {
        Task UploadAsync(
            string objectKey,
            Stream content,
            long contentLength,
            string contentType,
            CancellationToken cancellationToken = default);

        Task<StoredFileDto?> GetAsync(
            string objectKey,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            string objectKey,
            CancellationToken cancellationToken = default);
    }

    public sealed class StoredFileDto
    {
        public Stream Content { get; init; } = Stream.Null;
        public string ContentType { get; init; } = "application/octet-stream";
    }
}
