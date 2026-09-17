using FinalProject_Store.Domain.Entities.Common;
using FinalProject_Store.Domain.Entities.Orders;

namespace FinalProject_Store.Domain.Entities.Payments;

public enum PaymentStatus { Pending = 1, Succeeded = 2, Failed = 3, Cancelled = 4 }

public class Payment : BaseEntity
{
    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string Gateway { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
