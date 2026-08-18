using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using FinalProject_Store.Domain.Entities.Products;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Products.Commands.AddProduct
{
    public class AddProductService : IAddProductService
    {
        private readonly IDataBaseContext _context;

        public AddProductService(IDataBaseContext context)
        {
            _context = context;
        }

        public ResultDto Execute(AddProductDto request)
        {
            if (request == null)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "اطلاعات محصول ارسال نشده است."
                };
            }

            request.Name = request.Name?.Trim();
            request.Brand = request.Brand?.Trim();
            request.Description = request.Description?.Trim();
            request.ImageSrc = request.ImageSrc?.Trim();

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

            var categoryExists = _context.Categories
                .AsNoTracking()
                .Any(category =>
                    category.Id == request.CategoryId &&
                    category.IsActive);

            if (!categoryExists)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "دسته‌بندی انتخاب‌شده معتبر نیست."
                };
            }

            var duplicateProductExists = _context.Products
                .IgnoreQueryFilters()
                .Any(product =>
                    product.Name == request.Name &&
                    product.Brand == request.Brand &&
                    product.IsRemoved == false);

            if (duplicateProductExists)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "محصولی با این نام و برند قبلاً ثبت شده است."
                };
            }

            var product = new Product
            {
                Name = request.Name,
                Brand = request.Brand,
                Description = request.Description,
                Price = request.Price,
                Inventory = request.Inventory,
                ImageSrc = request.ImageSrc,
                IsActive = request.IsActive,
                CategoryId = request.CategoryId,
                InsertTime = DateTime.Now
            };

            _context.Products.Add(product);
            _context.SaveChanges();

            return new ResultDto
            {
                IsSuccess = true,
                Message = "محصول با موفقیت ثبت شد."
            };
        }
    }
}