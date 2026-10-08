using System.Security.Claims;
using FinalProject_Store.Application.Services.Orders;
using FinalProject_Store.Domain.Entities.Orders;
using Microsoft.AspNetCore.Mvc;

namespace EndPoint.Site.Areas.Admin.Controllers;

public class OrdersController(IAdminOrderService orders, IOrderLifecycleService lifecycle) : AdminBaseController
{
    [HttpGet]
    public IActionResult Index(string? search, OrderStatus? status, int page = 1) => View(orders.Orders(search, status, page));

    [HttpGet]
    public IActionResult Details(long id)
    {
        var order = orders.Details(id);
        return order == null ? NotFound() : View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Advance(long id, OrderStatus target)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = lifecycle.Advance(id, target, long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        TempData["OrderMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }
}
