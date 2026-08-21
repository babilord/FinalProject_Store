using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Products.Commands.EditProduct
{
    public interface IEditProductService { ResultDto Execute(EditProductDto request); }

    public class EditProductService : IEditProductService
    {
        private readonly IDataBaseContext _context;
        public EditProductService(IDataBaseContext context) { _context = context; }

        public ResultDto Execute(EditProductDto request)
        {
            if (request == null || request.Id <= 0) return Failure("اطلاعات محصول معتبر نیست.");
            request.Name = request.Name?.Trim();
            request.Brand = request.Brand?.Trim() ?? string.Empty;
            request.Description = request.Description?.Trim() ?? string.Empty;

            var validation = Validate(request);
            if (validation != null) return validation;

            var product = _context.Products.FirstOrDefault(item => item.Id == request.Id);
            if (product == null) return Failure("محصول موردنظر پیدا نشد.");

            var categoryExists = _context.Categories.AsNoTracking()
                .Any(category => category.Id == request.CategoryId && category.IsActive);
            if (!categoryExists) return Failure("دسته‌بندی انتخاب‌شده معتبر یا فعال نیست.");

            var duplicateExists = _context.Products.IgnoreQueryFilters()
                .Any(item => item.Id != request.Id && !item.IsRemoved &&
                    item.Name == request.Name && item.Brand == request.Brand);
            if (duplicateExists) return Failure("محصول دیگری با این نام و برند قبلاً ثبت شده است.");

            product.Name = request.Name;
            product.Brand = request.Brand;
            product.Description = request.Description;
            product.Price = request.Price;
            product.Inventory = request.Inventory;
            product.CategoryId = request.CategoryId;
            product.IsActive = request.IsActive;
            product.UpdateDate = DateTime.Now;
            _context.SaveChanges();
            return new ResultDto { IsSuccess = true, Message = "محصول با موفقیت ویرایش شد." };
        }

        private static ResultDto Validate(EditProductDto request)
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

        private static ResultDto Failure(string message) => new ResultDto { IsSuccess = false, Message = message };
    }

    public class EditProductDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int Inventory { get; set; }
        public long CategoryId { get; set; }
        public bool IsActive { get; set; }
    }
}
