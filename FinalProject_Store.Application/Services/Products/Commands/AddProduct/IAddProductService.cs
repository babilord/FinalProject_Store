using FinalProject_Store.Common.Dto;

namespace FinalProject_Store.Application.Services.Products.Commands.AddProduct
{
    public interface IAddProductService
    {
        ResultDto Execute(AddProductDto request);
    }

    public class AddProductDto
    {
        public string Name { get; set; }

        public string Brand { get; set; }

        public string Description { get; set; }

        public decimal Price { get; set; }

        public int Inventory { get; set; }

        public string ImageSrc { get; set; }

        public bool IsActive { get; set; } = true;

        public long CategoryId { get; set; }
    }
}
