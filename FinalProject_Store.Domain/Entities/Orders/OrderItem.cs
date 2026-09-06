using FinalProject_Store.Domain.Entities.Common;
using FinalProject_Store.Domain.Entities.Products;

namespace FinalProject_Store.Domain.Entities.Orders;

public class OrderItem : BaseEntity
{
    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
