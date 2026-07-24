using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using FinalProject_Store.Domain.Entities.Products;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Products.Categories.Commands.AddCategory
{
    public interface IAddCategoryService
    {
        ResultDto Execute(string name);
    }

    public class AddCategoryService : IAddCategoryService
    {
        private readonly IDataBaseContext _context;

        public AddCategoryService(IDataBaseContext context)
        {
            _context = context;
        }

        public ResultDto Execute(string name)
        {
            name = name?.Trim();

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

            bool categoryExists = _context.Categories
                .IgnoreQueryFilters()
                .Any(category =>
                    category.Name == name &&
                    category.IsRemoved == false);

            if (categoryExists)
            {
                return new ResultDto
                {
                    IsSuccess = false,
                    Message = "دسته‌بندی با این نام قبلاً ثبت شده است."
                };
            }

            var category = new Category
            {
                Name = name,
                IsActive = true,
                InsertTime = DateTime.Now
            };

            _context.Categories.Add(category);
            _context.SaveChanges();

            return new ResultDto
            {
                IsSuccess = true,
                Message = "دسته‌بندی با موفقیت ثبت شد."
            };
        }
    }
}