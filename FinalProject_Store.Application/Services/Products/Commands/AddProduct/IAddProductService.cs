using FinalProject_Store.Common.Dto;
using FinalProject_Store.Application.Services.Products.Common;

namespace FinalProject_Store.Application.Services.Products.Commands.AddProduct
{
    public interface IAddProductService
    {
        Task<ResultDto> ExecuteAsync(AddProductDto request, CancellationToken cancellationToken = default);
    }

    public class AddProductDto
    {
        public string Name { get; set; } = string.Empty;

        public string Brand { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int Inventory { get; set; }

        public ProductImageUploadDto? Image { get; set; }

        public bool IsActive { get; set; } = true;

        public long CategoryId { get; set; }
    }
}
