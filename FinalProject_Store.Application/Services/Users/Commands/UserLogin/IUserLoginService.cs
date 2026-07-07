using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;

namespace FinalProject_Store.Application.Services.Users.Commands.UserLogin
{
    public interface IUserLoginService
    {
        ResultDto<ResultUserLoginDto> Execute(RequestUserLoginDto request);
    }

    public class UserLoginService : IUserLoginService
    {
        private readonly IDataBaseContext _context;

        public UserLoginService(IDataBaseContext context)
        {
            _context = context;
        }

        public ResultDto<ResultUserLoginDto> Execute(RequestUserLoginDto request)
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

            var user = _context.Users.FirstOrDefault(p =>
                p.Email == request.Email &&
                p.Password == request.Password &&
                p.IsRemoved == false);

            if (user == null)
            {
                return new ResultDto<ResultUserLoginDto>
                {
                    IsSuccess = false,
                    Message = "کاربری با این مشخصات یافت نشد"
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

            return new ResultDto<ResultUserLoginDto>
            {
                IsSuccess = true,
                Message = "ورود با موفقیت انجام شد",
                Data = new ResultUserLoginDto
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email
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
    }
}