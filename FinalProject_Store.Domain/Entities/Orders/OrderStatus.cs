namespace FinalProject_Store.Domain.Entities.Orders;

public enum OrderStatus
{
    PendingPayment = 1,
    Paid = 2,
    Cancelled = 3,
    Processing = 4,
    Shipped = 5,
    Delivered = 6
}
