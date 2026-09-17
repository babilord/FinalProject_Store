using System.Security.Claims;
using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Domain.Entities.Orders;
using FinalProject_Store.Domain.Entities.Payments;
using FinalProject_Store.Infrastructures.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EndPoint.Site.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class FakePaymentsController(IDataBaseContext context, FakePaymentGateway gateway, IWebHostEnvironment environment) : Controller
{
    [HttpGet]
    public IActionResult Simulate(string token)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var payment = Load(token);
        return payment == null ? NotFound() : View(payment);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Complete(string token, PaymentStatus outcome)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var payment = Load(token);
        if (payment == null) return NotFound();
        if (outcome is not (PaymentStatus.Succeeded or PaymentStatus.Failed or PaymentStatus.Cancelled)) return BadRequest();
        var receipt = gateway.IssueReceipt(payment.Token, payment.OrderId, payment.Amount, payment.ExpiresAtUtc, outcome);
        return RedirectToAction("Callback", "Payments", new { token = payment.Token, receipt });
    }

    private Payment? Load(string token)
    {
        if (!environment.IsDevelopment() || string.IsNullOrWhiteSpace(token) || token.Length > 200 ||
            !long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
        return context.Payments.AsNoTracking().SingleOrDefault(x => x.Token == token && x.Gateway == gateway.Name &&
            x.Order.UserId == userId && x.Order.User.isActive && !x.Order.User.IsRemoved &&
            x.Order.Status == OrderStatus.PendingPayment && x.Status == PaymentStatus.Pending && x.ExpiresAtUtc > DateTime.UtcNow);
    }
}
