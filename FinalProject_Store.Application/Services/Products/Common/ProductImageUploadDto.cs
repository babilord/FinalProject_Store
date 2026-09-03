namespace FinalProject_Store.Application.Services.Products.Common
{
    public sealed class ProductImageUploadDto
    {
        public Stream Content { get; init; } = Stream.Null;
        public long Length { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
    }
}
