using System.Security.Cryptography;
using System.Text.Json;
using FinalProject_Store.Application.Interfaces.Payments;
using FinalProject_Store.Domain.Entities.Payments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;

namespace FinalProject_Store.Infrastructures.Payments;

public class FakePaymentGateway(IDataProtectionProvider protection, IHostEnvironment environment) : IPaymentGateway
{
    private readonly IDataProtector _protector = protection.CreateProtector("KalaMarket.FakePayment.v1");
    public string Name => "Fake";

    public Task<PaymentRequestResult> RequestAsync(long orderId, decimal amount)
    {
        EnsureDevelopment();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return Task.FromResult(new PaymentRequestResult(token,
            "/FakePayments/Simulate?token=" + token, DateTime.UtcNow.AddMinutes(30)));
    }

    // Only the authenticated, antiforgery-protected simulator issues receipts.
    // It never writes payment or order state. The callback verifies this protected payload.
    public string IssueReceipt(string token, long orderId, decimal amount, DateTime expiresAtUtc, PaymentStatus outcome)
    {
        EnsureDevelopment();
        if (outcome is not (PaymentStatus.Succeeded or PaymentStatus.Failed or PaymentStatus.Cancelled))
            throw new ArgumentOutOfRangeException(nameof(outcome));
        return _protector.Protect(JsonSerializer.Serialize(new Receipt(token, orderId, amount,
            expiresAtUtc, outcome, outcome == PaymentStatus.Succeeded ? "FAKE-" + token : null)));
    }

    public Task<PaymentVerification> VerifyAsync(string token, string receipt)
    {
        EnsureDevelopment();
        try
        {
            var data = JsonSerializer.Deserialize<Receipt>(_protector.Unprotect(receipt));
            if (data != null && data.Token == token && data.ExpiresAtUtc > DateTime.UtcNow &&
                data.Status is PaymentStatus.Succeeded or PaymentStatus.Failed or PaymentStatus.Cancelled)
                return Task.FromResult(new PaymentVerification(true, data.OrderId, data.Amount, data.Status, data.ReferenceId));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException) { }
        return Task.FromResult(new PaymentVerification(false, 0, 0, PaymentStatus.Failed, null));
    }

    private void EnsureDevelopment()
    {
        if (!environment.IsDevelopment()) throw new InvalidOperationException("Fake payments are available only in Development.");
    }

    private record Receipt(string Token, long OrderId, decimal Amount, DateTime ExpiresAtUtc,
        PaymentStatus Status, string? ReferenceId);
}
