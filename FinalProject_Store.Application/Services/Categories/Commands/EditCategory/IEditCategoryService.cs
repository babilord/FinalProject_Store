using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Categories.Commands.EditCategory
{
    public interface IEditCategoryService
    {
        ResultDto Execute(long id, string name);
    }

    public class EditCategoryService : IEditCategoryService
    {
        private readonly IDataBaseContext _context;

        public EditCategoryService(IDataBaseContext context)
        {
            _context = context;
        }

        public ResultDto Execute(long id, string name)
        {
            name = name?.Trim();

            if (id <= 0)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "شناسه دسته‌بندی نامعتبر است."
                };
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "نام دسته‌بندی را وارد کنید."
                };
            }

            if (name.Length < 2)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "نام دسته‌بندی باید حداقل دو کاراکتر باشد."
                };
            }

            if (name.Length > 200)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "نام دسته‌بندی نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد."
                };
            }

            var category = _context.Categories
                .FirstOrDefault(category => category.Id == id);

            if (category == null)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "دسته‌بندی موردنظر پیدا نشد."
                };
            }

            bool duplicateNameExists = _context.Categories
                .IgnoreQueryFilters()
                .Any(category =>
                    category.Id != id &&
                    category.Name == name &&
                    category.IsRemoved == false);

            if (duplicateNameExists)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "دسته‌بندی دیگری با این نام وجود دارد."
                };
            }

            category.Name = name;
            category.UpdateDate = DateTime.Now;

            _context.SaveChanges();

            return new ResultDto
            {
                IsSuccess = true,
                Message = "دسته‌بندی با موفقیت ویرایش شد."
            };
        }
    }
}