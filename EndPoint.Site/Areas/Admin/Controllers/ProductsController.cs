using EndPoint.Site.Areas.Admin.Models.Products;
using FinalProject_Store.Application.Services.Categories.Queries.GetActiveCategories;
using FinalProject_Store.Application.Services.Products.Commands.AddProduct;
using FinalProject_Store.Application.Services.Products.Queries;
using FinalProject_Store.Application.Services.Products.Commands.EditProduct;
using FinalProject_Store.Application.Services.Products.Commands.ProductStatusChange;
using FinalProject_Store.Application.Services.Products.Commands.RemoveProduct;
using FinalProject_Store.Application.Services.Products.Queries.GetProductDetails;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EndPoint.Site.Areas.Admin.Controllers
{
    public class ProductsController : AdminBaseController
    {
        private readonly IAddProductService _addProductService;
        private readonly IGetActiveCategoriesService _getActiveCategoriesService;
        private readonly IGetProductsService _getProductsService;
        private readonly IGetProductDetailsService _getProductDetailsService;
        private readonly IEditProductService _editProductService;
        private readonly IProductStatusChangeService _productStatusChangeService;
        private readonly IRemoveProductService _removeProductService;
        public ProductsController(
            IAddProductService addProductService,
            IGetActiveCategoriesService getActiveCategoriesService,
            IGetProductsService getProductsService,
            IGetProductDetailsService getProductDetailsService,
            IEditProductService editProductService,
            IProductStatusChangeService productStatusChangeService,
            IRemoveProductService removeProductService)
        {
            _addProductService = addProductService;
            _getActiveCategoriesService = getActiveCategoriesService;
            _getProductsService = getProductsService;
            _getProductDetailsService = getProductDetailsService;
            _editProductService = editProductService;
            _productStatusChangeService = productStatusChangeService;
            _removeProductService = removeProductService;
        }
        [HttpGet]
        public IActionResult Index(string searchKey, int page = 1)
        {
            return View(_getProductsService.Execute(new GetProductsRequestDto
            {
                SearchKey = searchKey,
                Page = page
            }));
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

        [HttpGet]
        public IActionResult Edit(long id)
        {
            var result = _getProductDetailsService.Execute(id);
            if (!result.IsSuccess) return NotFound();

            var model = new EditProductViewModel
            {
                Id = result.Data.Id,
                Name = result.Data.Name,
                Brand = result.Data.Brand,
                Description = result.Data.Description,
                Price = result.Data.Price,
                Inventory = result.Data.Inventory,
                CategoryId = result.Data.CategoryId,
                IsActive = result.Data.IsActive
            };
            SetCategories(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EditProductViewModel model)
        {
            if (!ModelState.IsValid)
            {
                SetCategories(model);
                return View(model);
            }

            var result = _editProductService.Execute(new EditProductDto
            {
                Id = model.Id, Name = model.Name, Brand = model.Brand,
                Description = model.Description, Price = model.Price,
                Inventory = model.Inventory, CategoryId = model.CategoryId,
                IsActive = model.IsActive
            });
            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                SetCategories(model);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult StatusChange(long productId)
        {
            return Json(_productStatusChangeService.Execute(productId));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(long productId)
        {
            return Json(_removeProductService.Execute(productId));
        }

        private void SetCategories(EditProductViewModel model)
        {
            model.Categories = _getActiveCategoriesService.Execute()
                .Select(category => new SelectListItem
                {
                    Value = category.Id.ToString(), Text = category.Name
                }).ToList();
        }
    }
}
