using FinalProject_Store.Application.Services.Users.Commands.EditUser;
using FinalProject_Store.Application.Services.Users.Commands.RegisterUser;
using FinalProject_Store.Application.Services.Users.Commands.RemoveUser;
using FinalProject_Store.Application.Services.Users.Commands.UserStatusChange;
using FinalProject_Store.Application.Services.Users.Queries.GetRoles;
using FinalProject_Store.Application.Services.Users.Queries.GetUsers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
//
namespace EndPoint.Site.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class UsersController : Controller   
    {
        private readonly IGetUsersService _getUsersService;
        private readonly IGetRolesService _getRolesService;
        private readonly IRegisterUserService _registerUserService;
        private readonly IRemoveUserService _removeUserService;
        private readonly IUserStatusChangeService _userStatusChangeService;
        private readonly IEditUserService _editUserService;
        public UsersController(IGetUsersService getUsersService
                             , IGetRolesService getRolesService
                             , IRegisterUserService registerUserService
                             , IRemoveUserService removeUserService
                             , IUserStatusChangeService userStatusChangeService
                             , IEditUserService edituserService)
        {
            _getUsersService = getUsersService;
            _getRolesService = getRolesService;
            _registerUserService = registerUserService;
            _removeUserService = removeUserService;
            _userStatusChangeService = userStatusChangeService;
            _editUserService = edituserService;
        }
        public IActionResult Index(string searchkey, int page = 1)
        {
            return View(_getUsersService.Execute(new RequestGetUserDto
            {
                Page = page,
                SearchKey = searchkey,
            }));
        }
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Roles = new SelectList(_getRolesService.Execute().Data, "Id", "Name");
            return View();
        }

        // kare actione Create ke alan misazam ine ke vaghti dar forme sabtenam data vared mikonim oon ro be in
        // action ersal mikone ke methode poste va dar in action miad data ro be service registerUser ersal mikone
        // va sabte nam ro anjam mide
        [HttpPost]
        public IActionResult Create(string Email, string FullName, long RoleId, string Password, string RePassword)
        {
            var result = _registerUserService.Execute(new RequestRegisterUserDto
            {
                Email = Email,
                FullName = FullName,
                roles = new List<RolesInRegisterUserDto>()
                {
                    new RolesInRegisterUserDto
                    {
                        Id = RoleId,
                    }
                },
                Password = Password,
                RePassword = RePassword,
            });
            return Json (result);
        }
        [HttpPost]
        public IActionResult Delete(long UserId)
        {
            return Json(_removeUserService.Execute(UserId));
        }

        [HttpPost]
        public IActionResult UserStatusChange(long UserId)
        {
            return Json(_userStatusChangeService.Execute(UserId));
        }

        [HttpPost]
        public IActionResult Edit(long UserId, string FullName, string Email)
        {
            var result = _editUserService.Execute(new RequestEditUserDto
            {
                UserId = UserId,
                FullName = FullName,
                Email = Email
            });

            return Json(result);
        }
    }
}
