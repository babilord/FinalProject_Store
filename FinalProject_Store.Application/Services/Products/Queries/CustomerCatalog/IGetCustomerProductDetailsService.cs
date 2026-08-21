using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Products.Queries.CustomerCatalog
{
    public interface IGetCustomerProductDetailsService
    {
        ResultDto<CustomerProductDetailsDto> Execute(long productId);
    }

    public class GetCustomerProductDetailsService : IGetCustomerProductDetailsService
    {
        private readonly IDataBaseContext _context;

        public GetCustomerProductDetailsService(IDataBaseContext context)
        {
            _context = context;
        }

        public ResultDto<CustomerProductDetailsDto> Execute(long productId)
        {
            if (productId <= 0)
            {
                return NotFoundResult();
            }

            var product = _context.Products
                .AsNoTracking()
                .Where(item => item.Id == productId && !item.IsRemoved && item.IsActive && item.Inventory > 0)
                .Select(item => new CustomerProductDetailsDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    Brand = item.Brand,
                    Description = item.Description,
                    Price = item.Price,
                    Inventory = item.Inventory,
                    CategoryName = item.Category.Name,
                    ImageSrc = item.ImageSrc
                })
                .FirstOrDefault();

            if (product == null)
            {
                return NotFoundResult();
            }

            return new ResultDto<CustomerProductDetailsDto>
            {
                IsSuccess = true,
                Message = string.Empty,
                Data = product
            };
        }

        private static ResultDto<CustomerProductDetailsDto> NotFoundResult()
        {
            return new ResultDto<CustomerProductDetailsDto>
            {
                IsSuccess = false,
                Message = "محصول موردنظر یافت نشد یا در حال حاضر قابل عرضه نیست."
            };
        }
    }

    public class CustomerProductDetailsDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Inventory { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string ImageSrc { get; set; } = string.Empty;
    }
}
