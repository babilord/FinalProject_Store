using FinalProject_Store.Domain.Entities.Payments;

namespace FinalProject_Store.Application.Interfaces.Payments;

// Amounts use the store's currency (toman). Providers own any currency conversion.
public interface IPaymentGateway
{
    string Name { get; }
    Task<PaymentRequestResult> RequestAsync(long orderId, decimal amount);
    Task<PaymentVerification> VerifyAsync(string token, string receipt);
}

public record PaymentRequestResult(string Token, string RedirectUrl, DateTime ExpiresAtUtc);
public record PaymentVerification(bool IsVerified, long OrderId, decimal Amount,
    PaymentStatus Status, string? ReferenceId);
