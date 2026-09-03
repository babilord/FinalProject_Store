using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Application.Interfaces.Storage;
using FinalProject_Store.Application.Services.Products.Common;
using FinalProject_Store.Common.Dto;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Products.Commands.EditProduct
{
    public interface IEditProductService
    {
        Task<ResultDto> ExecuteAsync(EditProductDto request, CancellationToken cancellationToken = default);
    }

    public class EditProductService : IEditProductService
    {
        private readonly IDataBaseContext _context;
        private readonly IFileStorageService _fileStorageService;

        public EditProductService(IDataBaseContext context, IFileStorageService fileStorageService)
        {
            _context = context;
            _fileStorageService = fileStorageService;
        }

        public async Task<ResultDto> ExecuteAsync(EditProductDto request, CancellationToken cancellationToken = default)
        {
            if (request == null || request.Id <= 0) return Failure("اطلاعات محصول معتبر نیست.");
            request.Name = request.Name?.Trim() ?? string.Empty;
            request.Brand = request.Brand?.Trim() ?? string.Empty;
            request.Description = request.Description?.Trim() ?? string.Empty;

            var validation = Validate(request);
            if (validation != null) return validation;

            var product = await _context.Products.FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);
            if (product == null) return Failure("محصول موردنظر پیدا نشد.");

            var categoryExists = await _context.Categories.AsNoTracking()
                .AnyAsync(category => category.Id == request.CategoryId && category.IsActive, cancellationToken);
            if (!categoryExists) return Failure("دسته‌بندی انتخاب‌شده معتبر یا فعال نیست.");

            var duplicateExists = await _context.Products.IgnoreQueryFilters()
                .AnyAsync(item => item.Id != request.Id && !item.IsRemoved &&
                    item.Name == request.Name && item.Brand == request.Brand, cancellationToken);
            if (duplicateExists) return Failure("محصول دیگری با این نام و برند قبلاً ثبت شده است.");

            var oldImageObjectKey = product.ImageSrc;
            var newImageObjectKey = string.Empty;
            if (request.Image != null)
            {
                var imageValidation = await ProductImageValidator.ValidateAsync(request.Image, cancellationToken);
                if (!imageValidation.IsSuccess) return Failure(imageValidation.Message);

                newImageObjectKey = $"products/{Guid.NewGuid():N}{imageValidation.Data}";
                try
                {
                    await _fileStorageService.UploadAsync(newImageObjectKey, request.Image.Content,
                        request.Image.Length, request.Image.ContentType, cancellationToken);
                }
                catch
                {
                    return Failure("بارگذاری تصویر جدید انجام نشد؛ تصویر قبلی حفظ شده است.");
                }
            }

            product.Name = request.Name;
            product.Brand = request.Brand;
            product.Description = request.Description;
            product.Price = request.Price;
            product.Inventory = request.Inventory;
            product.CategoryId = request.CategoryId;
            product.IsActive = request.IsActive;
            if (!string.IsNullOrWhiteSpace(newImageObjectKey)) product.ImageSrc = newImageObjectKey;
            product.UpdateDate = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(newImageObjectKey))
                {
                    try { await _fileStorageService.DeleteAsync(newImageObjectKey, cancellationToken); }
                    catch { }
                }
                return Failure("ویرایش محصول انجام نشد؛ تصویر قبلی حفظ شده است.");
            }

            if (!string.IsNullOrWhiteSpace(newImageObjectKey) && !string.IsNullOrWhiteSpace(oldImageObjectKey))
            {
                try { await _fileStorageService.DeleteAsync(oldImageObjectKey, cancellationToken); }
                catch { }
            }

            return new ResultDto { IsSuccess = true, Message = "محصول با موفقیت ویرایش شد." };
        }

        private static ResultDto? Validate(EditProductDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return Failure("نام محصول را وارد کنید.");
            if (request.Name.Length < 2) return Failure("نام محصول باید حداقل دو کاراکتر باشد.");
            if (request.Name.Length > 300) return Failure("نام محصول نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد.");
            if (request.Brand.Length > 200) return Failure("نام برند نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.");
            if (request.Description.Length > 4000) return Failure("توضیحات نمی‌تواند بیشتر از ۴۰۰۰ کاراکتر باشد.");
            if (request.Price <= 0) return Failure("قیمت محصول باید بیشتر از صفر باشد.");
            if (request.Inventory < 0) return Failure("موجودی محصول نمی‌تواند منفی باشد.");
            if (request.CategoryId <= 0) return Failure("دسته‌بندی محصول را انتخاب کنید.");
            return null;
        }

        private static ResultDto Failure(string message) => new() { IsSuccess = false, Message = message };
    }

    public class EditProductDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Inventory { get; set; }
        public long CategoryId { get; set; }
        public bool IsActive { get; set; }
        public ProductImageUploadDto? Image { get; set; }
    }
}
