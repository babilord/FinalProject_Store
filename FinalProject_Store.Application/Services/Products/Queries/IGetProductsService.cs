using FinalProject_Store.Application.Interfaces.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Products.Queries
{
    public interface IGetProductsService
    {
        GetProductsResultDto Execute(GetProductsRequestDto request);
    }

    public class GetProductsService : IGetProductsService
    {
        private readonly IDataBaseContext _context;

        public GetProductsService(IDataBaseContext context)
        {
            _context = context;
        }

        public GetProductsResultDto Execute(GetProductsRequestDto request)
        {
            const int pageSize = 10;
            request ??= new GetProductsRequestDto();
            var page = request.Page < 1 ? 1 : request.Page;
            var searchKey = request.SearchKey?.Trim();

            var query = _context.Products
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                query = query.Where(product =>
                    product.Name.Contains(searchKey) ||
                    product.Brand.Contains(searchKey));
            }

            var rowsCount = query.Count();
            var pageCount = (int)Math.Ceiling(rowsCount / (double)pageSize);
            if (pageCount > 0 && page > pageCount)
                page = pageCount;

            var products = query
                .OrderByDescending(product => product.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(product => new GetProductDto
                {
                    Id = product.Id,
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
                Products = products,
                SearchKey = searchKey,
                Page = page,
                PageSize = pageSize,
                RowsCount = rowsCount
            };
        }
    }

    public class GetProductsRequestDto
    {
        public string SearchKey { get; set; }
        public int Page { get; set; } = 1;
    }

    public class GetProductsResultDto
    {
        public List<GetProductDto> Products { get; set; }
            = new List<GetProductDto>();
        public string SearchKey { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int RowsCount { get; set; }
        public int PageCount => (int)Math.Ceiling(RowsCount / (double)PageSize);
    }

    public class GetProductDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public string Brand { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int Inventory { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
