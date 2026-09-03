using FinalProject_Store.Domain.Entities.Common;
using FinalProject_Store.Domain.Entities.Products;

namespace FinalProject_Store.Domain.Entities.Carts
{
    public class CartItem : BaseEntity
    {
        public long CartId { get; set; }
        public Cart Cart { get; set; } = null!;
        public long ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public int Quantity { get; set; }
    }
}
