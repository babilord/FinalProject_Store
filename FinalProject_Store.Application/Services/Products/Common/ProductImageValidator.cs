using FinalProject_Store.Common.Dto;

namespace FinalProject_Store.Application.Services.Products.Common
{
    internal static class ProductImageValidator
    {
        public const long MaximumFileSize = 5 * 1024 * 1024;

        private static readonly IReadOnlyDictionary<string, string> AllowedTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
                [".webp"] = "image/webp"
            };

        public static async Task<ResultDto<string>> ValidateAsync(
            ProductImageUploadDto image,
            CancellationToken cancellationToken)
        {
            if (image.Length <= 0)
                return Failure("فایل تصویر خالی است.");

            if (image.Length > MaximumFileSize)
                return Failure("حجم تصویر نباید بیشتر از ۵ مگابایت باشد.");

            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!AllowedTypes.TryGetValue(extension, out var expectedContentType) ||
                !string.Equals(image.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
            {
                return Failure("فرمت تصویر باید JPG، JPEG، PNG یا WEBP باشد.");
            }

            if (!image.Content.CanRead || !image.Content.CanSeek)
                return Failure("فایل تصویر قابل خواندن نیست.");

            var header = new byte[12];
            var originalPosition = image.Content.Position;
            var bytesRead = await image.Content.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
            image.Content.Position = originalPosition;

            if (!MatchesSignature(extension, header.AsSpan(0, bytesRead)))
                return Failure("محتوای فایل با فرمت تصویر انتخاب‌شده مطابقت ندارد.");

            return new ResultDto<string>
            {
                IsSuccess = true,
                Data = extension,
                Message = string.Empty
            };
        }

        private static bool MatchesSignature(string extension, ReadOnlySpan<byte> header)
        {
            if (extension is ".jpg" or ".jpeg")
                return header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            if (extension == ".png")
                return header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            return extension == ".webp" && header.Length >= 12 &&
                   header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8);
        }

        private static ResultDto<string> Failure(string message) => new()
        {
            IsSuccess = false,
            Message = message,
            Data = string.Empty
        };
    }
}
