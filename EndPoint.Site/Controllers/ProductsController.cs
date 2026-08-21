using FinalProject_Store.Application.Services.Products.Queries.CustomerCatalog;
using Microsoft.AspNetCore.Mvc;

namespace EndPoint.Site.Controllers
{
    public class ProductsController : Controller
    {
        private readonly IGetCustomerProductsService _getCustomerProductsService;
        private readonly IGetCustomerProductDetailsService _getCustomerProductDetailsService;

        public ProductsController(
            IGetCustomerProductsService getCustomerProductsService,
            IGetCustomerProductDetailsService getCustomerProductDetailsService)
        {
            _getCustomerProductsService = getCustomerProductsService;
            _getCustomerProductDetailsService = getCustomerProductDetailsService;
        }

        [HttpGet]
        public IActionResult Index(string? searchKey, long? categoryId, int page = 1)
        {
            var result = _getCustomerProductsService.Execute(new CustomerProductsRequestDto
            {
                SearchKey = searchKey,
                CategoryId = categoryId,
                Page = page
            });

            return View(result);
        }

        [HttpGet]
        public IActionResult Details(long id)
        {
            var result = _getCustomerProductDetailsService.Execute(id);
            if (!result.IsSuccess)
            {
                return NotFound();
            }

            return View(result.Data);
        }
    }
}
