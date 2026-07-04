using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Application.Services.Users.Commands.EditUser;
using FinalProject_Store.Application.Services.Users.Commands.RegisterUser;
using FinalProject_Store.Application.Services.Users.Commands.RemoveUser;
using FinalProject_Store.Application.Services.Users.Commands.UserStatusChange;
using FinalProject_Store.Application.Services.Users.Queries.GetRoles;
using FinalProject_Store.Application.Services.Users.Queries.GetUsers;
using FinalProject_Store.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<DataBaseContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IDataBaseContext>(provider => provider.GetRequiredService<DataBaseContext>());
builder.Services.AddScoped<IGetUsersService, GetUsersService>();
builder.Services.AddScoped<IGetRolesService, GetRolesService>();
builder.Services.AddScoped<IRegisterUserService, RegisterUserService>();
builder.Services.AddScoped<IRemoveUserService, RemoveUserService>();
builder.Services.AddScoped<IUserStatusChangeService, UserStatusChangeService>();
builder.Services.AddScoped<IEditUserService, EditUserService>();

var app = builder.Build();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
  name: "areas",
  pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);

app.Run();
