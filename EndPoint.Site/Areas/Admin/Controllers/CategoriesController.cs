using Microsoft.AspNetCore.Mvc;
using FinalProject_Store.Application.Services.Categories.Commands;
using FinalProject_Store.Application.Services.Categories.Queries;
using FinalProject_Store.Application.Services.Categories.Commands.EditCategory;

namespace EndPoint.Site.Areas.Admin.Controllers
{
    public class CategoriesController : AdminBaseController
    {
        private readonly IGetCategoriesService _getCategoriesService;
        private readonly IAddCategoryService _addCategoryService;
        private readonly IEditCategoryService _editCategoryService;

        public CategoriesController(
            IGetCategoriesService getCategoriesService,
            IAddCategoryService addCategoryService,
            IEditCategoryService editCategoryService)
        {
            _getCategoriesService = getCategoriesService;
            _addCategoryService = addCategoryService;
            _editCategoryService = editCategoryService;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(long id, string name)
        {
            var result = _editCategoryService.Execute(id, name);

            return Json(result);
        }

    }
}