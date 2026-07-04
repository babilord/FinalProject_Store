using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalProject_Store.Application.Services.Users.Commands.UserStatusChange
{
    public interface IUserStatusChangeService
    {
        ResultDto Execute(long userId);
    }
    public class UserStatusChangeService : IUserStatusChangeService
    {
        private readonly IDataBaseContext _context;
        public UserStatusChangeService(IDataBaseContext context)
        {
            _context = context;
        }
        public ResultDto Execute(long userId)
        {
            try
            {
                var user = _context.Users.Find(userId);

                if (user == null)
                {
                    return new ResultDto()
                    {
                        IsSuccess = false,
                        Message = "کاربر یافت نشد"
                    };
                }
                user.isActive = !user.isActive;
                user.UpdateDate = DateTime.Now;

                _context.SaveChanges();

                return new ResultDto()
                {
                    IsSuccess = true,
                    Message = $"وضعیت کاربر با موفقیت به {(user.isActive ? "فعال" : "غیرفعال")} تغییر یافت."
                };
            }
            catch (Exception ex)
            {
                return new ResultDto()
                {
                    IsSuccess = false,
                    Message = $"خطا در تغییر وضعیت کاربر: {ex.Message}"
                };
            }
        }
    }
}
