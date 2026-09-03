using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Products.Queries.GetProductDetails
{
    public interface IGetProductDetailsService { ResultDto<ProductDetailsDto> Execute(long id); }

    public class GetProductDetailsService : IGetProductDetailsService
    {
        private readonly IDataBaseContext _context;
        public GetProductDetailsService(IDataBaseContext context) { _context = context; }

        public ResultDto<ProductDetailsDto> Execute(long id)
        {
            var product = _context.Products.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new ProductDetailsDto
                {
                    Id = item.Id, Name = item.Name, Brand = item.Brand,
                    Description = item.Description, Price = item.Price,
                    Inventory = item.Inventory, CategoryId = item.CategoryId,
                    ImageSrc = item.ImageSrc,
                    IsActive = item.IsActive
                }).FirstOrDefault();

            return new ResultDto<ProductDetailsDto>
            {
                IsSuccess = product != null,
                Message = product == null ? "محصول موردنظر پیدا نشد." : string.Empty,
                Data = product
            };
        }
    }

    public class ProductDetailsDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int Inventory { get; set; }
        public long CategoryId { get; set; }
        public bool IsActive { get; set; }
        public string ImageSrc { get; set; } = string.Empty;
    }
}
