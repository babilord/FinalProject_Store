using FinalProject_Store.Application.Interfaces.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Categories.Queries.GetActiveCategories
{
    public interface IGetActiveCategoriesService
    {
        List<ActiveCategoryDto> Execute();
    }

    public class GetActiveCategoriesService : IGetActiveCategoriesService
    {
        private readonly IDataBaseContext _context;

        public GetActiveCategoriesService(IDataBaseContext context)
        {
            _context = context;
        }

        public List<ActiveCategoryDto> Execute()
        {
            return _context.Categories
                .AsNoTracking()
                .Where(category => category.IsActive)
                .OrderBy(category => category.Name)
                .Select(category => new ActiveCategoryDto
                {
                    Id = category.Id,
                    Name = category.Name
                })
                .ToList();
        }
    }

    public class ActiveCategoryDto
    {
        public long Id { get; set; }

        public string Name { get; set; }
    }
}