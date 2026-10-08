using System.Data;
using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using FinalProject_Store.Domain.Entities.Orders;
using FinalProject_Store.Domain.Entities.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinalProject_Store.Application.Services.Orders;

public interface IOrderLifecycleService
{
    ResultDto Cancel(long userId, long orderId);
    bool Expire(long orderId);
    ResultDto Advance(long orderId, OrderStatus target, long adminId);
}

public class OrderLifecycleService(IDataBaseContext context, ILogger<OrderLifecycleService> logger) : IOrderLifecycleService
{
    public ResultDto Cancel(long userId, long orderId) => CancelCore(orderId, userId, false);
    public bool Expire(long orderId) => CancelCore(orderId, null, true).IsSuccess;

    private ResultDto CancelCore(long orderId, long? userId, bool expirationOnly)
    {
        try
        {
            using var transaction = context.BeginTransaction(IsolationLevel.Serializable);
            context.LockOrder(orderId);
            // Even removed orders/products must release stock. Historical item quantities are authoritative.
            var order = context.Orders.IgnoreQueryFilters().Include(x => x.Items).Include(x => x.Payments)
                .SingleOrDefault(x => x.Id == orderId);
            if (order == null || (userId.HasValue && (order.IsRemoved || order.UserId != userId ||
                !context.Users.Any(x => x.Id == userId && x.isActive))))
                return Result(false, "سفارش موردنظر یافت نشد.");
            if (order.Status == OrderStatus.Cancelled) return Result(!expirationOnly, "سفارش قبلاً لغو شده است.");
            if (order.Status != OrderStatus.PendingPayment)
                return Result(false, "لغو سفارش پرداخت‌شده به بازپرداخت نیاز دارد و در این نسخه امکان‌پذیر نیست.");
            var expired = order.ExpiresAtUtc <= DateTime.UtcNow;
            if (expirationOnly && !expired) return Result(false, "مهلت سفارش تمام نشده است.");
            if (order.Payments.Any(x => x.Status == PaymentStatus.Succeeded))
                throw new InvalidOperationException("Pending order has a successful payment.");

            foreach (var item in order.Items.GroupBy(x => x.ProductId).OrderBy(x => x.Key))
            {
                var quantity = item.Sum(x => checked(x.Quantity));
                if (quantity <= 0 || item.Any(x => x.Quantity <= 0)) throw new InvalidOperationException("Invalid reservation quantity.");
                var product = context.Products.IgnoreQueryFilters().Single(x => x.Id == item.Key);
                product.Inventory = checked(product.Inventory + quantity);
                product.UpdateDate = DateTime.Now;
            }
            order.Status = OrderStatus.Cancelled;
            order.ReservationExpired = expired;
            order.UpdateDate = DateTime.Now;
            foreach (var payment in order.Payments.Where(x => x.Status == PaymentStatus.Pending))
            {
                payment.Status = PaymentStatus.Cancelled;
                payment.UpdateDate = DateTime.Now;
            }
            context.SaveChanges();
            transaction.Commit();
            logger.LogInformation("Order {OrderId} cancelled; expired {Expired}; inventory restored from {ItemCount} items", orderId, expired, order.Items.Count);
            return Result(true, expired ? "مهلت پرداخت تمام شد؛ سفارش لغو و موجودی آزاد شد." : "سفارش لغو و موجودی آزاد شد.");
        }
        catch (Exception ex)
        {
            context.ClearTracking();
            logger.LogError(ex, "Could not cancel/expire order {OrderId}; transaction rolled back", orderId);
            return Result(false, "لغو سفارش انجام نشد؛ لطفاً دوباره تلاش کنید.");
        }
    }

    public ResultDto Advance(long orderId, OrderStatus target, long adminId)
    {
        try
        {
            using var transaction = context.BeginTransaction(IsolationLevel.Serializable);
            context.LockOrder(orderId);
            var order = context.Orders.SingleOrDefault(x => x.Id == orderId);
            if (order == null || !OrderRules.CanAdvance(order.Status, target))
                return Result(false, "تغییر وضعیت درخواستی برای این سفارش مجاز نیست.");
            var previous = order.Status;
            order.Status = target;
            order.UpdateDate = DateTime.Now;
            context.SaveChanges();
            transaction.Commit();
            logger.LogInformation("Admin {AdminId} advanced order {OrderId} from {Previous} to {Target}", adminId, orderId, previous, target);
            return Result(true, "وضعیت سفارش به‌روز شد.");
        }
        catch (Exception ex)
        {
            context.ClearTracking();
            logger.LogError(ex, "Fulfillment update failed for order {OrderId}", orderId);
            return Result(false, "تغییر وضعیت انجام نشد؛ دوباره تلاش کنید.");
        }
    }

    private static ResultDto Result(bool success, string message) => new() { IsSuccess = success, Message = message };
}

public static class OrderRules
{
    public static bool CanAdvance(OrderStatus from, OrderStatus to) => (from, to) is
        (OrderStatus.Paid, OrderStatus.Processing) or (OrderStatus.Processing, OrderStatus.Shipped) or
        (OrderStatus.Shipped, OrderStatus.Delivered);
    public static string Label(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => "در انتظار پرداخت", OrderStatus.Paid => "پرداخت شده",
        OrderStatus.Processing => "در حال پردازش", OrderStatus.Shipped => "ارسال شده",
        OrderStatus.Delivered => "تحویل شده", OrderStatus.Cancelled => "لغو شده", _ => "نامشخص"
    };
    public static string Label(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "در انتظار پرداخت", PaymentStatus.Succeeded => "موفق",
        PaymentStatus.Failed => "ناموفق", PaymentStatus.Cancelled => "لغو شده", _ => "نامشخص"
    };
}
