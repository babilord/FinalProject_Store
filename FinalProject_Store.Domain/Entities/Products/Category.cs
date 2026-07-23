using FinalProject_Store.Domain.Entities.Common;

namespace FinalProject_Store.Domain.Entities.Products
{
    public class Category : BaseEntity
    {
        public string Name { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Product> Products { get; set; }
            = new List<Product>();
    }
}