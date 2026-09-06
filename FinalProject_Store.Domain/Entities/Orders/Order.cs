using FinalProject_Store.Domain.Entities.Common;
using FinalProject_Store.Domain.Entities.Users;

namespace FinalProject_Store.Domain.Entities.Orders;

public class Order : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;
    public decimal Total { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalAddress { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
