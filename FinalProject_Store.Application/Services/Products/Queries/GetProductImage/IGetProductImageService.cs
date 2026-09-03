using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Application.Interfaces.Storage;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Products.Queries.GetProductImage
{
    public interface IGetProductImageService
    {
        Task<StoredFileDto?> ExecuteAsync(long productId, CancellationToken cancellationToken = default);
    }

    public sealed class GetProductImageService : IGetProductImageService
    {
        private readonly IDataBaseContext _context;
        private readonly IFileStorageService _fileStorageService;

        public GetProductImageService(IDataBaseContext context, IFileStorageService fileStorageService)
        {
            _context = context;
            _fileStorageService = fileStorageService;
        }

        public async Task<StoredFileDto?> ExecuteAsync(long productId, CancellationToken cancellationToken = default)
        {
            if (productId <= 0)
                return null;

            var objectKey = await _context.Products
                .AsNoTracking()
                .Where(product => product.Id == productId)
                .Select(product => product.ImageSrc)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(objectKey))
                return null;

            return await _fileStorageService.GetAsync(objectKey, cancellationToken);
        }
    }
}
