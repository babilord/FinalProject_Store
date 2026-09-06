using System.Security.Claims;
using FinalProject_Store.Application.Services.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EndPoint.Site.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly IOrderService _orderService;
    public OrdersController(IOrderService orderService) => _orderService = orderService;

    [HttpGet]
    public IActionResult Checkout()
    {
        var result = _orderService.GetCheckout(CurrentUserId());
        if (!result.IsSuccess)
        {
            TempData["CartError"] = result.Message;
            return RedirectToAction("Index", "Cart");
        }
        return View(new CheckoutViewModel { Summary = result.Data });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Checkout([Bind(Prefix = "Input")] CreateOrderDto input)
    {
        var summary = _orderService.GetCheckout(CurrentUserId());
        if (!summary.IsSuccess)
        {
            TempData["CartError"] = summary.Message;
            return RedirectToAction("Index", "Cart");
        }
        if (!ModelState.IsValid)
            return View(new CheckoutViewModel { Input = input, Summary = summary.Data });

        var result = _orderService.Create(CurrentUserId(), input);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            var refreshed = _orderService.GetCheckout(CurrentUserId());
            if (!refreshed.IsSuccess)
            {
                TempData["CartError"] = result.Message;
                return RedirectToAction("Index", "Cart");
            }
            return View(new CheckoutViewModel { Input = input, Summary = refreshed.Data });
        }
        TempData["OrderSuccess"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = result.Data });
    }

    [HttpGet]
    public IActionResult Details(long id)
    {
        var result = _orderService.GetDetails(CurrentUserId(), id);
        return result.IsSuccess ? View(result.Data) : NotFound();
    }

    [HttpGet]
    public IActionResult Index() => View(_orderService.GetMyOrders(CurrentUserId()).Data);

    private long CurrentUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public class CheckoutViewModel
{
    public CreateOrderDto Input { get; set; } = new();
    public CheckoutDto Summary { get; set; } = new();
}
