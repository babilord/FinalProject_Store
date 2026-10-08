using System.Data;
using Microsoft.Extensions.Logging;
using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Application.Interfaces.Payments;
using FinalProject_Store.Common.Dto;
using FinalProject_Store.Domain.Entities.Orders;
using FinalProject_Store.Domain.Entities.Payments;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Payments;

public interface IPaymentService
{
    Task<ResultDto<string>> PayAsync(long userId, long orderId);
    Task<ResultDto<long>> CallbackAsync(string token, string receipt);
}

public class PaymentService(IDataBaseContext context, IPaymentGateway gateway, ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<ResultDto<string>> PayAsync(long userId, long orderId)
    {
        try
        {
            using var transaction = context.BeginTransaction(IsolationLevel.Serializable);
            context.LockOrder(orderId);
            var order = context.Orders.Include(x => x.Items)
                .SingleOrDefault(x => x.Id == orderId && x.UserId == userId && x.User.isActive && !x.User.IsRemoved);
            if (userId <= 0 || order == null || order.Status != OrderStatus.PendingPayment || order.ExpiresAtUtc <= DateTime.UtcNow || !ValidAmount(order))
                return Fail<string>("این سفارش قابل پرداخت نیست.");

            var pending = context.Payments.SingleOrDefault(x => x.OrderId == orderId && x.Status == PaymentStatus.Pending);
            if (pending != null && pending.ExpiresAtUtc > DateTime.UtcNow && pending.Gateway == gateway.Name && pending.Amount == order.Total)
                return Ok(pending.RedirectUrl);
            if (pending != null)
            {
                pending.Status = PaymentStatus.Cancelled;
                pending.UpdateDate = DateTime.Now;
                context.SaveChanges(); // Release the filtered unique index before inserting the retry.
            }

            var request = await gateway.RequestAsync(order.Id, order.Total);
            context.Payments.Add(new Payment { OrderId = order.Id, Amount = order.Total,
                Gateway = gateway.Name, Token = request.Token, RedirectUrl = request.RedirectUrl,
                ExpiresAtUtc = request.ExpiresAtUtc < order.ExpiresAtUtc ? request.ExpiresAtUtc : order.ExpiresAtUtc });
            context.SaveChanges();
            transaction.Commit();
            logger.LogInformation("Payment requested for order {OrderId}", orderId);
            return Ok(request.RedirectUrl);
        }
        catch (Exception ex) when (ex is DbUpdateException or System.Data.Common.DbException or InvalidOperationException)
        {
            context.ClearTracking();
            logger.LogWarning(ex, "Payment request failed for order {OrderId}", orderId);
            return Fail<string>("شروع پرداخت انجام نشد. لطفاً دوباره تلاش کنید.");
        }
    }

    public async Task<ResultDto<long>> CallbackAsync(string token, string receipt)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200 || string.IsNullOrWhiteSpace(receipt) || receipt.Length > 4096)
        {
            logger.LogWarning("Malformed payment callback rejected");
            return Fail<long>("درخواست پرداخت معتبر نیست.");
        }
        try
        {
            var orderId = context.Payments.AsNoTracking().Where(x => x.Token == token).Select(x => (long?)x.OrderId).SingleOrDefault();
            if (orderId == null) { logger.LogWarning("Unknown payment callback"); return Fail<long>("پرداخت یافت نشد."); }
            using var transaction = context.BeginTransaction(IsolationLevel.Serializable);
            context.LockOrder(orderId.Value);
            var payment = context.Payments.Include(x => x.Order).ThenInclude(x => x.Items)
                .SingleOrDefault(x => x.Token == token);
            if (payment == null || payment.Gateway != gateway.Name)
                return Fail<long>("پرداخت یافت نشد.");
            var verified = await gateway.VerifyAsync(payment.Token, receipt);
            // Invalid/untrusted callbacks cannot consume a legitimate pending attempt.
            if (!verified.IsVerified || verified.OrderId != payment.OrderId || verified.Amount != payment.Amount)
            {
                logger.LogWarning("Invalid verification for payment {PaymentId}", payment.Id);
                return Fail<long>("تأیید پرداخت انجام نشد. از صفحه سفارش دوباره تلاش کنید.");
            }
            if (payment.Order.Status == OrderStatus.Cancelled)
                return Ok(payment.OrderId, payment.Order.ReservationExpired
                    ? "مهلت پرداخت تمام شده و سفارش لغو شده است." : "این سفارش لغو شده و قابل پرداخت نیست.");
            if (payment.Status != PaymentStatus.Pending)
                return Ok(payment.OrderId, Message(payment.Status));
            if (payment.Order.IsRemoved || !context.Users.Any(x => x.Id == payment.Order.UserId && x.isActive) ||
                payment.Order.Status != OrderStatus.PendingPayment || payment.Order.ExpiresAtUtc <= DateTime.UtcNow || !ValidAmount(payment.Order) ||
                payment.Amount != payment.Order.Total || payment.ExpiresAtUtc <= DateTime.UtcNow)
                return Fail<long>("سفارش یا مبلغ پرداخت معتبر نیست یا مهلت پرداخت تمام شده است.");
            if (verified.Status is not (PaymentStatus.Succeeded or PaymentStatus.Failed or PaymentStatus.Cancelled) ||
                (verified.Status == PaymentStatus.Succeeded && string.IsNullOrWhiteSpace(verified.ReferenceId)))
                return Fail<long>("پاسخ درگاه معتبر نیست.");

            payment.Status = verified.Status;
            payment.ReferenceId = verified.Status == PaymentStatus.Succeeded ? verified.ReferenceId : null;
            payment.UpdateDate = DateTime.Now;
            if (verified.Status == PaymentStatus.Succeeded)
            {
                payment.Order.Status = OrderStatus.Paid;
                payment.Order.UpdateDate = DateTime.Now;
            }
            context.SaveChanges();
            transaction.Commit();
            logger.LogInformation("Payment {PaymentId} for order {OrderId} completed with {Status}", payment.Id, payment.OrderId, payment.Status);
            return Ok(payment.OrderId, Message(payment.Status));
        }
        catch (Exception ex) when (ex is DbUpdateException or System.Data.Common.DbException or InvalidOperationException)
        {
            context.ClearTracking();
            logger.LogWarning(ex, "Payment callback transaction failed");
            return Fail<long>("ثبت نتیجه پرداخت انجام نشد. همین درخواست را دوباره باز کنید.");
        }
    }

    private static bool ValidAmount(Order order) => order.Total > 0 && order.Items.Count > 0 &&
        order.Items.All(x => x.Quantity > 0 && x.UnitPrice >= 0 && x.LineTotal == x.UnitPrice * x.Quantity) &&
        order.Total == order.Items.Sum(x => x.LineTotal);
    private static string Message(PaymentStatus status) => status switch
    {
        PaymentStatus.Succeeded => "پرداخت با موفقیت تأیید شد.",
        PaymentStatus.Cancelled => "از پرداخت انصراف داده شد. می‌توانید دوباره تلاش کنید.",
        _ => "پرداخت ناموفق بود. می‌توانید دوباره تلاش کنید."
    };
    private static ResultDto<T> Ok<T>(T data, string message = "") => new() { IsSuccess = true, Data = data, Message = message };
    private static ResultDto<T> Fail<T>(string message) => new() { IsSuccess = false, Data = default!, Message = message };
}
