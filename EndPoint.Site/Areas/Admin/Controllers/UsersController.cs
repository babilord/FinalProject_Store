using FinalProject_Store.Application.Services.Users.Queries.GetRoles;
using FinalProject_Store.Application.Services.Users.Queries.GetUsers;
using FinalProject_Store.Application.Services.Users.Queries.GetRoles;
using FinalProject_Store.Application.Services.Users.Queries.GetUsers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EndPoint.Site.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class UsersController : Controller
    {
        private readonly IGetUsersService _getUsersService;
        private readonly IGetRolesService _getRolesService;
        public UsersController(IGetUsersService getUsersService, IGetRolesService getRolesService)
        {
            _getUsersService = getUsersService;
            _getRolesService = getRolesService;
        }


        public IActionResult Index(string serchkey, int page = 1)
        {
            return View(_getUsersService.Execute(new RequestGetUserDto
            {
                Page = page,
                SearchKey = serchkey,
            }));
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Roles = new SelectList(_getRolesService.Execute().Data, "Id", "Name");
            return View();
        }



    }
}
