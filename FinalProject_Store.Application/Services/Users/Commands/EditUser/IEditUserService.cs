using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;

namespace FinalProject_Store.Application.Services.Users.Commands.EditUser
{
    public interface IEditUserService
    {
        ResultDto Execute(RequestEditUserDto request);
    }

    public class EditUserService : IEditUserService
    {
        private readonly IDataBaseContext _context;

        public EditUserService(IDataBaseContext context)
        {
            _context = context;
        }

        public ResultDto Execute(RequestEditUserDto request)
        {
            try
            {
                var user = _context.Users.Find(request.UserId);

                if (user == null)
                {
                    return new ResultDto()
                    {
                        IsSuccess = false,
                        Message = "کاربر یافت نشد"
                    };
                }

                if (string.IsNullOrWhiteSpace(request.FullName))
                {
                    return new ResultDto()
                    {
                        IsSuccess = false,
                        Message = "نام و نام خانوادگی را وارد نمایید"
                    };
                }

                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    return new ResultDto()
                    {
                        IsSuccess = false,
                        Message = "ایمیل را وارد نمایید"
                    };
                }

                var isEmailExists = _context.Users
                    .Any(p => p.Email == request.Email && p.Id != request.UserId);

                if (isEmailExists)
                {
                    return new ResultDto()
                    {
                        IsSuccess = false,
                        Message = "این ایمیل برای کاربر دیگری ثبت شده است"
                    };
                }

                user.FullName = request.FullName;
                user.Email = request.Email;
                user.UpdateDate = DateTime.Now;

                _context.SaveChanges();

                return new ResultDto()
                {
                    IsSuccess = true,
                    Message = "ویرایش کاربر با موفقیت انجام شد"
                };
            }
            catch (Exception ex)
            {
                return new ResultDto()
                {
                    IsSuccess = false,
                    Message = $"خطا در ویرایش کاربر: {ex.Message}"
                };
            }
        }
    }

    public class RequestEditUserDto
    {
        public long UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
    }
}