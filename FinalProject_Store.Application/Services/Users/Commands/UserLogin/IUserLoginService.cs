using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Application.Interfaces.Security;
using FinalProject_Store.Common.Dto;

namespace FinalProject_Store.Application.Services.Users.Commands.UserLogin
{
    public interface IUserLoginService
    {
        ResultDto<ResultUserLoginDto> Execute(
            RequestUserLoginDto request);
    }

    public class UserLoginService : IUserLoginService
    {
        private readonly IDataBaseContext _context;
        private readonly IPasswordHasher _passwordHasher;

        public UserLoginService(
            IDataBaseContext context,
            IPasswordHasher passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public ResultDto<ResultUserLoginDto> Execute(
            RequestUserLoginDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return new ResultDto<ResultUserLoginDto>
                {
                    IsSuccess = false,
                    Message = "ایمیل را وارد نمایید"
                };
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return new ResultDto<ResultUserLoginDto>
                {
                    IsSuccess = false,
                    Message = "رمز عبور را وارد نمایید"
                };
            }

            string normalizedEmail =
                request.Email.Trim().ToLower();

            var user = _context.Users.FirstOrDefault(p =>
                p.Email.ToLower() == normalizedEmail &&
                p.IsRemoved == false);

            if (user == null)
            {
                return new ResultDto<ResultUserLoginDto>
                {
                    IsSuccess = false,
                    Message = "ایمیل یا رمز عبور اشتباه است"
                };
            }

            bool passwordIsValid;

            if (_passwordHasher.IsHashed(user.Password))
            {
                passwordIsValid =
                    _passwordHasher.VerifyPassword(
                        user.Password,
                        request.Password);
            }
            else
            {
                // پشتیبانی موقت از کاربران قدیمی
                passwordIsValid =
                    user.Password == request.Password;

                if (passwordIsValid)
                {
                    user.Password =
                        _passwordHasher.HashPassword(
                            request.Password);

                    user.UpdateDate = DateTime.Now;

                    _context.SaveChanges();
                }
            }

            if (!passwordIsValid)
            {
                return new ResultDto<ResultUserLoginDto>
                {
                    IsSuccess = false,
                    Message = "ایمیل یا رمز عبور اشتباه است"
                };
            }

            if (!user.isActive)
            {
                return new ResultDto<ResultUserLoginDto>
                {
                    IsSuccess = false,
                    Message = "حساب کاربری شما غیرفعال است"
                };
            }

            //  دریافت نقش‌های کاربر برای کوکی ها
            var roleNames = (
                from userInRole in _context.UserInRoles
                join role in _context.Roles
                    on userInRole.RoleId equals role.Id
                where userInRole.UserId == user.Id
                select role.Name
            )
            .Distinct()
            .ToList();

            if (!roleNames.Any())
            {
                return new ResultDto<ResultUserLoginDto>
                {
                    IsSuccess = false,
                    Message = "برای این کاربر هیچ نقشی تعیین نشده است"
                };
            }

            return new ResultDto<ResultUserLoginDto>
            {
                IsSuccess = true,
                Message = "ورود با موفقیت انجام شد",

                Data = new ResultUserLoginDto
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Roles = roleNames
                }
            };
        }
    }

    public class RequestUserLoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class ResultUserLoginDto
    {
        public long UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }

        public List<string> Roles { get; set; } = new();
    }
}