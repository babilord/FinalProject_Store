using FinalProject_Store.Application.Interfaces.Security;
using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using FinalProject_Store.Domain.Entities.Users;
using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using FinalProject_Store.Domain.Entities.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalProject_Store.Application.Services.Users.Commands.RegisterUser
{
    public interface IRegisterUserService      
    {
        ResultDto<ResultRegisterUserDto> Execute(RequestRegisterUserDto request); // in ya'ni ma yek gharardad tarif mikonim ke har servicei ke mikhad register user ro anjam bede bayad
                                                                                  // methode Execute ro dashte bashe va in methode Execute yek request ro migire va yek result ro bar migardoone
    }   
    public class RegisterUserService : IRegisterUserService
    {
        private readonly IDataBaseContext _context;
        private readonly IPasswordHasher _passwordHasher;
        public RegisterUserService(
    IDataBaseContext context,
    IPasswordHasher passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }
        public ResultDto<ResultRegisterUserDto> Execute(RequestRegisterUserDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    return new ResultDto<ResultRegisterUserDto>()
                    {
                        Data = new ResultRegisterUserDto()
                        {
                            UserId = 0,
                        },
                        IsSuccess = false,
                        Message = "پست الکترونیک را وارد نمایید"
                    };
                }

                if (string.IsNullOrWhiteSpace(request.FullName))
                {
                    return new ResultDto<ResultRegisterUserDto>()
                    {
                        Data = new ResultRegisterUserDto()
                        {
                            UserId = 0,
                        },
                        IsSuccess = false,
                        Message = "نام را وارد نمایید"
                    };
                }
                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    return new ResultDto<ResultRegisterUserDto>()
                    {
                        Data = new ResultRegisterUserDto()
                        {
                            UserId = 0,
                        },
                        IsSuccess = false,
                        Message = "رمز عبور را وارد نمایید"
                    };
                }
                if (request.Password != request.RePassword)
                {
                    return new ResultDto<ResultRegisterUserDto>()
                    {
                        Data = new ResultRegisterUserDto()
                        {
                            UserId = 0,
                        },
                        IsSuccess = false,
                        Message = "رمز عبور و تکرار آن برابر نیست"
                    };
                }

                var normalizedEmail = request.Email.Trim().ToLower();

                var userExists = _context.Users.Any(
                    p => p.Email.ToLower() == normalizedEmail);

                if (userExists)
                {
                    return new ResultDto<ResultRegisterUserDto>()
                    {
                        Data = new ResultRegisterUserDto()
                        {
                            UserId = 0
                        },
                        IsSuccess = false,
                        Message = "این ایمیل قبلاً ثبت شده است"
                    };
                }

                User user = new User()
                {
                    Email = normalizedEmail,
                    FullName = request.FullName.Trim(),
                    Password = _passwordHasher.HashPassword(request.Password),
                    isActive = true
                };

                List<UserInRole> userInRoles = new List<UserInRole>();

                foreach (var item in request.roles)
                {
                    var roles = _context.Roles.Find(item.Id);

                    if (roles == null)
                    {
                        return new ResultDto<ResultRegisterUserDto>()
                        {
                            IsSuccess = false,
                            Message = "نقش انتخاب شده یافت نشد"
                        };
                    }


                    userInRoles.Add(new UserInRole
                    {
                        Role = roles,
                        RoleId = roles.Id,
                        User = user,
                        UserId = user.Id,
                    });
                }
                user.UserInRoles = userInRoles;

                _context.Users.Add(user);

                var count = _context.SaveChanges();

               // _context.SaveChanges();

                return new ResultDto<ResultRegisterUserDto>()
                {
                    Data = new ResultRegisterUserDto()
                    {
                        UserId = user.Id,

                    },
                    IsSuccess = true,
                    Message = "ثبت نام کاربر انجام شد",
                };
            }
            //catch (Exception ex)
            //{
            //    return new ResultDto<ResultRegisterUserDto>()
            //    {
            //        IsSuccess = false,
            //        Message = ex.ToString()
            //    };
            //}
            catch (Exception)
            {
                return new ResultDto<ResultRegisterUserDto>()
                {
                    Data = new ResultRegisterUserDto()
                    {
                        UserId = 0,
                    },
                    IsSuccess = false,
                    Message = "ثبت نام انجام نشد !"
                };
            }
        }
    }
    public class RequestRegisterUserDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string RePassword { get; set; }
        public List<RolesInRegisterUserDto> roles { get; set; }
    }

    public class RolesInRegisterUserDto
    {
        public long Id { get; set; }
    }

    public class ResultRegisterUserDto
    {
        public long UserId { get; set; }

    }
}
