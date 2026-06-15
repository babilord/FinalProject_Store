using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;

namespace FinalProject_Store.Application.Services.Users.Commands.RemoveUser
{
    public class RemoveUserService : IRemoveUserService
    {
        private readonly IDataBaseContext _context;
        public RemoveUserService(IDataBaseContext context)
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
                user.IsRemoved = true;
                user.RemoveTime = DateTime.Now;
                _context.SaveChanges();
                return new ResultDto()
                {
                    IsSuccess = true,
                    Message = "کاربر با موفقیت حذف شد"
                };
            }
            catch (Exception ex)
            {
                return new ResultDto()
                {
                    IsSuccess = false,
                    Message = $"خطا در حذف کاربر: {ex.Message}"
                };
            }
        }
    }
}
