using FinalProject_Store.Application.Services.Users.Commands.RegisterUser;
using FinalProject_Store.Application.Services.Users.Commands.UserLogin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EndPoint.Site.Controllers
{
    public class AuthenticationController : Controller
    {
        private readonly IRegisterUserService _registerUserService;
        private readonly IUserLoginService _userLoginService;

        public AuthenticationController(
            IRegisterUserService registerUserService,
            IUserLoginService userLoginService)
        {
            _registerUserService = registerUserService;
            _userLoginService = userLoginService;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(string FullName, string Email, string Password, string RePassword)
        {
            var result = _registerUserService.Execute(new RequestRegisterUserDto
            {
                FullName = FullName,
                Email = Email,
                Password = Password,
                RePassword = RePassword,
                roles = new List<RolesInRegisterUserDto>
                {
                    new RolesInRegisterUserDto
                    {
                        Id = 3
                    }
                }
            });

            return Json(result);
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = "/")
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.ReturnUrl = returnUrl;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(
    string Email,
    string Password,
    bool RememberMe,
    string returnUrl = "/")
        {
            var result = _userLoginService.Execute(
                new RequestUserLoginDto
                {
                    Email = Email,
                    Password = Password
                });

            if (!result.IsSuccess)
            {
                return Json(result);
            }

            var claims = new List<Claim>
    {
        new Claim(
            ClaimTypes.NameIdentifier,
            result.Data.UserId.ToString()),

        new Claim(
            ClaimTypes.Name,
            result.Data.FullName),

        new Claim(
            ClaimTypes.Email,
            result.Data.Email)
    };

            foreach (var role in result.Data.Roles)
            {
                claims.Add(
                    new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            var authenticationProperties =
                new AuthenticationProperties
                {
                    IsPersistent = RememberMe
                };

            if (RememberMe)
            {
                authenticationProperties.ExpiresUtc =
                    DateTimeOffset.UtcNow.AddDays(30);
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authenticationProperties);

            if (string.IsNullOrWhiteSpace(returnUrl) ||
                !Url.IsLocalUrl(returnUrl))
            {
                returnUrl = "/";
            }

            return Json(new
            {
                isSuccess = true,
                message = "ورود با موفقیت انجام شد",
                redirectUrl = returnUrl
            });
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}