using System.Security.Claims;
using FinalProject_Store.Application.Services.Carts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EndPoint.Site.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ICartService _cartService;
        public CartController(ICartService cartService) { _cartService = cartService; }

        [HttpGet]
        public IActionResult Index()
        {
            var result = _cartService.Get(CurrentUserId());
            if (!string.IsNullOrWhiteSpace(result.Message)) TempData["CartInfo"] = result.Message;
            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(long productId, int quantity = 1)
        {
            var result = _cartService.Add(CurrentUserId(), productId, quantity);
            SetFeedback(result.IsSuccess, result.Message);
            return RedirectToAction("Details", "Products", new { id = productId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateQuantity(long cartItemId, int quantity)
        {
            var result = _cartService.UpdateQuantity(CurrentUserId(), cartItemId, quantity);
            SetFeedback(result.IsSuccess, result.Message);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(long cartItemId)
        {
            var result = _cartService.Remove(CurrentUserId(), cartItemId);
            SetFeedback(result.IsSuccess, result.Message);
            return RedirectToAction(nameof(Index));
        }

        private long CurrentUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private void SetFeedback(bool success, string message) => TempData[success ? "CartSuccess" : "CartError"] = message;
    }
}
