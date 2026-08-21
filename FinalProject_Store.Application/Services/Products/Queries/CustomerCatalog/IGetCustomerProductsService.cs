using FinalProject_Store.Application.Interfaces.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Products.Queries.CustomerCatalog
{
    public interface IGetCustomerProductsService
    {
        CustomerProductsResultDto Execute(CustomerProductsRequestDto request);
    }

    public class GetCustomerProductsService : IGetCustomerProductsService
    {
        private const int PageSize = 12;
        private readonly IDataBaseContext _context;

        public GetCustomerProductsService(IDataBaseContext context)
        {
            _context = context;
        }

        public CustomerProductsResultDto Execute(CustomerProductsRequestDto request)
        {
            request ??= new CustomerProductsRequestDto();
            var page = request.Page < 1 ? 1 : request.Page;
            var searchKey = request.SearchKey?.Trim();
            var categoryId = request.CategoryId > 0 ? request.CategoryId : null;

            var query = _context.Products
                .AsNoTracking()
                .Where(product => !product.IsRemoved && product.IsActive && product.Inventory > 0);

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                query = query.Where(product =>
                    product.Name.Contains(searchKey) ||
                    product.Brand.Contains(searchKey));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(product => product.CategoryId == categoryId.Value);
            }

            var rowsCount = query.Count();
            var pageCount = (int)Math.Ceiling(rowsCount / (double)PageSize);
            if (pageCount > 0 && page > pageCount)
            {
                page = pageCount;
            }

            var products = query
                .OrderByDescending(product => product.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(product => new CustomerProductDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Brand = product.Brand,
                    Price = product.Price,
                    Inventory = product.Inventory,
                    CategoryName = product.Category.Name,
                    ImageSrc = product.ImageSrc
                })
                .ToList();

            var categories = _context.Categories
                .AsNoTracking()
                .Where(category => category.IsActive)
                .OrderBy(category => category.Name)
                .Select(category => new CustomerCategoryDto
                {
                    Id = category.Id,
                    Name = category.Name
                })
                .ToList();

            return new CustomerProductsResultDto
            {
                Products = products,
                Categories = categories,
                SearchKey = searchKey ?? string.Empty,
                CategoryId = categoryId,
                Page = page,
                PageSize = PageSize,
                RowsCount = rowsCount
            };
        }
    }

    public class CustomerProductsRequestDto
    {
        public string? SearchKey { get; set; }
        public long? CategoryId { get; set; }
        public int Page { get; set; } = 1;
    }

    public class CustomerProductsResultDto
    {
        public List<CustomerProductDto> Products { get; set; } = new();
        public List<CustomerCategoryDto> Categories { get; set; } = new();
        public string SearchKey { get; set; } = string.Empty;
        public long? CategoryId { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int RowsCount { get; set; }
        public int PageCount => PageSize == 0 ? 0 : (int)Math.Ceiling(RowsCount / (double)PageSize);
    }

    public class CustomerProductDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Inventory { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string ImageSrc { get; set; } = string.Empty;
    }

    public class CustomerCategoryDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
