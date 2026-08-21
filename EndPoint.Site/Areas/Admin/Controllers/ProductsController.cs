using EndPoint.Site.Areas.Admin.Models.Products;
using FinalProject_Store.Application.Services.Categories.Queries.GetActiveCategories;
using FinalProject_Store.Application.Services.Products.Commands.AddProduct;
using FinalProject_Store.Application.Services.Products.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EndPoint.Site.Areas.Admin.Controllers
{
    public class ProductsController : AdminBaseController
    {
        private readonly IAddProductService _addProductService;
        private readonly IGetActiveCategoriesService _getActiveCategoriesService;
        private readonly IGetProductsService _getProductsService;
        public ProductsController(
            IAddProductService addProductService,
            IGetActiveCategoriesService getActiveCategoriesService,
            IGetProductsService getProductsService)
        {
            _addProductService = addProductService;
            _getActiveCategoriesService = getActiveCategoriesService;
            _getProductsService = getProductsService;
        }
        [HttpGet]
        public IActionResult Index()
        {
            var result = _getProductsService.Execute();

            return View(result.Products);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var categories = _getActiveCategoriesService.Execute();

            var model = new CreateProductViewModel
            {
                Categories = categories
                    .Select(category => new SelectListItem
                    {
                        Value = category.Id.ToString(),
                        Text = category.Name
                    })
                    .ToList()
            };

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreateProductViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Categories = _getActiveCategoriesService.Execute()
                    .Select(category => new SelectListItem
                    {
                        Value = category.Id.ToString(),
                        Text = category.Name
                    })
                    .ToList();

                return View(model);
            }

            var result = _addProductService.Execute(new AddProductDto
            {
                Name = model.Name,
                Brand = model.Brand,
                Description = model.Description,
                Price = model.Price,
                Inventory = model.Inventory,
                CategoryId = model.CategoryId,
                IsActive = model.IsActive,
                ImageSrc = string.Empty
            });

            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.Message);

                model.Categories = _getActiveCategoriesService.Execute()
                    .Select(category => new SelectListItem
                    {
                        Value = category.Id.ToString(),
                        Text = category.Name
                    })
                    .ToList();

                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;

            return RedirectToAction(nameof(Create));
        }
    }
}
