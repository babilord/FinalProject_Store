using FinalProject_Store.Application.Services.Products.Categories.Commands.AddCategory;
using FinalProject_Store.Application.Services.Products.Categories.Queries.GetCategories;
using Microsoft.AspNetCore.Mvc;

namespace EndPoint.Site.Areas.Admin.Controllers
{
    public class CategoriesController : AdminBaseController
    {
        private readonly IGetCategoriesService _getCategoriesService;
        private readonly IAddCategoryService _addCategoryService;

        public CategoriesController(
            IGetCategoriesService getCategoriesService,
            IAddCategoryService addCategoryService)
        {
            _getCategoriesService = getCategoriesService;
            _addCategoryService = addCategoryService;
        }

        [HttpGet]
        public IActionResult Index(string searchKey)
        {
            var result = _getCategoriesService.Execute(searchKey);

            ViewBag.SearchKey = searchKey;

            return View(result.Categories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(string name)
        {
            var result = _addCategoryService.Execute(name);

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}