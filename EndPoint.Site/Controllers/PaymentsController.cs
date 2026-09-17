using System.Security.Claims;
using FinalProject_Store.Application.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EndPoint.Site.Controllers;

public class PaymentsController(IPaymentService payments) : Controller
{
    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(long orderId)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        var result = await payments.PayAsync(userId, orderId);
        if (result.IsSuccess) return Redirect(result.Data);
        TempData["PaymentMessage"] = result.Message;
        return RedirectToAction("Details", "Orders", new { id = orderId });
    }

    // Gateway callbacks cannot depend on a customer cookie or an antiforgery token.
    // The opaque receipt is verified by the gateway before any state transition.
    [AllowAnonymous, HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Callback(string token, string receipt)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var result = await payments.CallbackAsync(token, receipt);
        if (!result.IsSuccess) return BadRequest(result.Message);
        TempData["PaymentMessage"] = result.Message;
        return RedirectToAction("Details", "Orders", new { id = result.Data });
    }
}
