using EndPoint.Site.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace EndPoint.Site.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [IgnoreAntiforgeryToken]
        public IActionResult Status(int code)
        {
            Response.StatusCode = code is >= 400 and <= 599 ? code : 500;
            return View(Response.StatusCode);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [IgnoreAntiforgeryToken]
        public IActionResult Error()
        {
            Response.StatusCode = 500;
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
