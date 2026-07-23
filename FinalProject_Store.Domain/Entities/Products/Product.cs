using FinalProject_Store.Domain.Entities.Common;

namespace FinalProject_Store.Domain.Entities.Products
{
    public class Product : BaseEntity
    {
        public string Name { get; set; }

        public string Brand { get; set; }

        public string Description { get; set; }

        public decimal Price { get; set; }

        public int Inventory { get; set; }

        public string ImageSrc { get; set; }

        public bool IsActive { get; set; } = true;

        public long CategoryId { get; set; }

        public Category Category { get; set; }
    }
}