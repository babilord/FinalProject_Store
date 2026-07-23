using FinalProject_Store.Application.Interfaces.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Products.Categories.Queries.GetCategories
{
    public interface IGetCategoriesService
    {
        GetCategoriesResultDto Execute(string searchKey = null);
    }

    public class GetCategoriesService : IGetCategoriesService
    {
        private readonly IDataBaseContext _context;

        public GetCategoriesService(IDataBaseContext context)
        {
            _context = context;
        }

        public GetCategoriesResultDto Execute(string searchKey = null)
        {
            var categories = _context.Categories
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                searchKey = searchKey.Trim();

                categories = categories.Where(category =>
                    category.Name.Contains(searchKey));
            }

            var categoryList = categories
                .OrderByDescending(category => category.Id)
                .Select(category => new GetCategoryDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    IsActive = category.IsActive,
                    InsertTime = category.InsertTime,
                    ProductsCount = category.Products.Count()
                })
                .ToList();

            return new GetCategoriesResultDto
            {
                Categories = categoryList
            };
        }
    }

    public class GetCategoriesResultDto
    {
        public List<GetCategoryDto> Categories { get; set; }
            = new List<GetCategoryDto>();
    }

    public class GetCategoryDto
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public bool IsActive { get; set; }

        public int ProductsCount { get; set; }

        public DateTime InsertTime { get; set; }
    }
}