using FinalProject_Store.Application.Interfaces.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Products.Queries
{
    public interface IGetProductsService
    {
        GetProductsResultDto Execute();
    }

    public class GetProductsService : IGetProductsService
    {
        private readonly IDataBaseContext _context;

        public GetProductsService(IDataBaseContext context)
        {
            _context = context;
        }

        public GetProductsResultDto Execute()
        {
            var products = _context.Products
                .AsNoTracking()
                .OrderByDescending(product => product.Id)
                .Select(product => new GetProductDto
                {
                    Name = product.Name,
                    Brand = product.Brand,
                    Price = product.Price,
                    Inventory = product.Inventory,
                    CategoryName = product.Category.Name,
                    IsActive = product.IsActive
                })
                .ToList();

            return new GetProductsResultDto
            {
                Products = products
            };
        }
    }

    public class GetProductsResultDto
    {
        public List<GetProductDto> Products { get; set; }
            = new List<GetProductDto>();
    }

    public class GetProductDto
    {
        public string Name { get; set; } = string.Empty;

        public string Brand { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int Inventory { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
