using FinalProject_Store.Application.Services.Products.Queries.CustomerCatalog;
using Microsoft.AspNetCore.Mvc;
using FinalProject_Store.Application.Services.Products.Queries.GetProductImage;

namespace EndPoint.Site.Controllers
{
    public class ProductsController : Controller
    {
        private readonly IGetCustomerProductsService _getCustomerProductsService;
        private readonly IGetCustomerProductDetailsService _getCustomerProductDetailsService;
        private readonly IGetProductImageService _getProductImageService;

        public ProductsController(
            IGetCustomerProductsService getCustomerProductsService,
            IGetCustomerProductDetailsService getCustomerProductDetailsService,
            IGetProductImageService getProductImageService)
        {
            _getCustomerProductsService = getCustomerProductsService;
            _getCustomerProductDetailsService = getCustomerProductDetailsService;
            _getProductImageService = getProductImageService;
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

        [HttpGet]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> Image(long id, CancellationToken cancellationToken)
        {
            try
            {
                var image = await _getProductImageService.ExecuteAsync(id, cancellationToken);
                return image == null ? NotFound() : File(image.Content, image.ContentType);
            }
            catch
            {
                return NotFound();
            }
        }
    }
}
