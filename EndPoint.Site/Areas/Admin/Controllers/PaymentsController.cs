using FinalProject_Store.Application.Services.Orders;
using FinalProject_Store.Domain.Entities.Payments;
using Microsoft.AspNetCore.Mvc;

namespace EndPoint.Site.Areas.Admin.Controllers;

public class PaymentsController(IAdminOrderService orders) : AdminBaseController
{
    [HttpGet]
    public IActionResult Index(string? search, PaymentStatus? status, long? orderId, int page = 1) =>
        View(orders.Payments(search, status, orderId, page));
}
