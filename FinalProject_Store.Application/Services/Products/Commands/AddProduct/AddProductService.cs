using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using FinalProject_Store.Domain.Entities.Products;
using Microsoft.EntityFrameworkCore;
using FinalProject_Store.Application.Interfaces.Storage;
using FinalProject_Store.Application.Services.Products.Common;

namespace FinalProject_Store.Application.Services.Products.Commands.AddProduct
{
    public class AddProductService : IAddProductService
    {
        private readonly IDataBaseContext _context;
        private readonly IFileStorageService _fileStorageService;

        public AddProductService(IDataBaseContext context, IFileStorageService fileStorageService)
        {
            _context = context;
            _fileStorageService = fileStorageService;
        }

        public async Task<ResultDto> ExecuteAsync(AddProductDto request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "اطلاعات محصول ارسال نشده است."
                };
            }

            request.Name = request.Name?.Trim() ?? string.Empty;
            request.Brand = request.Brand?.Trim() ?? string.Empty;
            request.Description = request.Description?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "نام محصول را وارد کنید."
                };
            }

            if (request.Name.Length < 2)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "نام محصول باید حداقل دو کاراکتر باشد."
                };
            }

            if (request.Name.Length > 300)
            {
                return new ResultDto { IsSuccess = false, Message = "نام محصول نمی‌تواند بیشتر از ۳۰۰ کاراکتر باشد." };
            }

            request.Brand ??= string.Empty;
            request.Description ??= string.Empty;

            if (request.Brand.Length > 200)
            {
                return new ResultDto { IsSuccess = false, Message = "نام برند نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد." };
            }

            if (request.Description.Length > 4000)
            {
                return new ResultDto { IsSuccess = false, Message = "توضیحات نمی‌تواند بیشتر از ۴۰۰۰ کاراکتر باشد." };
            }

            if (request.Price <= 0)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "قیمت محصول باید بیشتر از صفر باشد."
                };
            }

            if (request.Inventory < 0)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "موجودی محصول نمی‌تواند منفی باشد."
                };
            }

            if (request.CategoryId <= 0)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "دسته‌بندی محصول را انتخاب کنید."
                };
            }

            var categoryExists = await _context.Categories
                .AsNoTracking()
                .AnyAsync(category =>
                    category.Id == request.CategoryId &&
                    category.IsActive, cancellationToken);

            if (!categoryExists)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "دسته‌بندی انتخاب‌شده معتبر نیست."
                };
            }

            var duplicateProductExists = await _context.Products
                .IgnoreQueryFilters()
                .AnyAsync(product =>
                    product.Name == request.Name &&
                    product.Brand == request.Brand &&
                    product.IsRemoved == false, cancellationToken);

            if (duplicateProductExists)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "محصولی با این نام و برند قبلاً ثبت شده است."
                };
            }

            var imageObjectKey = string.Empty;
            if (request.Image != null)
            {
                var imageValidation = await ProductImageValidator.ValidateAsync(request.Image, cancellationToken);
                if (!imageValidation.IsSuccess)
                    return new ResultDto { IsSuccess = false, Message = imageValidation.Message };

                imageObjectKey = $"products/{Guid.NewGuid():N}{imageValidation.Data}";
                try
                {
                    await _fileStorageService.UploadAsync(
                        imageObjectKey,
                        request.Image.Content,
                        request.Image.Length,
                        request.Image.ContentType,
                        cancellationToken);
                }
                catch
                {
                    return new ResultDto { IsSuccess = false, Message = "بارگذاری تصویر محصول انجام نشد. لطفاً دوباره تلاش کنید." };
                }
            }

            var product = new Product
            {
                Name = request.Name,
                Brand = request.Brand,
                Description = request.Description,
                Price = request.Price,
                Inventory = request.Inventory,
                ImageSrc = imageObjectKey,
                IsActive = request.IsActive,
                CategoryId = request.CategoryId,
                InsertTime = DateTime.Now
            };

            try
            {
                _context.Products.Add(product);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(imageObjectKey))
                {
                    try { await _fileStorageService.DeleteAsync(imageObjectKey, cancellationToken); }
                    catch { }
                }

                return new ResultDto { IsSuccess = false, Message = "ثبت محصول انجام نشد. لطفاً دوباره تلاش کنید." };
            }

            return new ResultDto
            {
                IsSuccess = true,
                Message = "محصول با موفقیت ثبت شد."
            };
        }
    }
}
