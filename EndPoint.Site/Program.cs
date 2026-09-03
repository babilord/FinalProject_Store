using FinalProject_Store.Application.Common.Security;
using FinalProject_Store.Application.Interfaces.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Application.Services.Users.Commands.EditUser;
using FinalProject_Store.Application.Services.Users.Commands.RegisterUser;
using FinalProject_Store.Application.Services.Users.Commands.RemoveUser;
using FinalProject_Store.Application.Services.Users.Commands.UserStatusChange;
using FinalProject_Store.Application.Services.Users.Queries.GetRoles;
using FinalProject_Store.Application.Services.Users.Queries.GetUsers;
using FinalProject_Store.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using FinalProject_Store.Application.Services.Users.Commands.UserLogin;
using FinalProject_Store.Application.Services.Products.Commands.AddProduct;
using FinalProject_Store.Application.Services.Categories.Commands;
using FinalProject_Store.Application.Services.Categories.Queries;
using FinalProject_Store.Application.Services.Categories.Commands.EditCategory;
using FinalProject_Store.Application.Services.Categories.Queries.GetActiveCategories;
using FinalProject_Store.Application.Services.Products.Queries;
using FinalProject_Store.Application.Services.Products.Commands.EditProduct;
using FinalProject_Store.Application.Services.Products.Commands.ProductStatusChange;
using FinalProject_Store.Application.Services.Products.Commands.RemoveProduct;
using FinalProject_Store.Application.Services.Products.Queries.GetProductDetails;
using FinalProject_Store.Application.Services.Products.Queries.CustomerCatalog;
using FinalProject_Store.Application.Interfaces.Storage;
using FinalProject_Store.Application.Services.Products.Queries.GetProductImage;
using FinalProject_Store.Infrastructures.Storage;
using Minio;
using FinalProject_Store.Application.Services.Carts;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddControllersWithViews();
// Authentication part
builder.Services.AddAuthentication(options =>
{
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Authentication/Login";
    options.LogoutPath = "/Authentication/Logout";
    options.AccessDeniedPath =
        "/Authentication/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});


builder.Services.AddDbContext<DataBaseContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IDataBaseContext>(provider => provider.GetRequiredService<DataBaseContext>());
builder.Services.AddScoped<IGetUsersService, GetUsersService>();
builder.Services.AddScoped<IGetRolesService, GetRolesService>();
builder.Services.AddScoped<IRegisterUserService, RegisterUserService>();
builder.Services.AddScoped<IRemoveUserService, RemoveUserService>();
builder.Services.AddScoped<IUserStatusChangeService, UserStatusChangeService>();
builder.Services.AddScoped<IEditUserService, EditUserService>();
builder.Services.AddScoped<IUserLoginService, UserLoginService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAddCategoryService, AddCategoryService>();
builder.Services.AddScoped<IGetCategoriesService, GetCategoriesService>();
builder.Services.AddScoped<IEditCategoryService, EditCategoryService>();
builder.Services.AddScoped<IAddProductService, AddProductService>();
builder.Services.AddScoped<IGetActiveCategoriesService,GetActiveCategoriesService>();
builder.Services.AddScoped<IGetProductsService, GetProductsService>();
builder.Services.AddScoped<IGetProductDetailsService, GetProductDetailsService>();
builder.Services.AddScoped<IEditProductService, EditProductService>();
builder.Services.AddScoped<IProductStatusChangeService, ProductStatusChangeService>();
builder.Services.AddScoped<IRemoveProductService, RemoveProductService>();
builder.Services.AddScoped<IGetCustomerProductsService, GetCustomerProductsService>();
builder.Services.AddScoped<IGetCustomerProductDetailsService, GetCustomerProductDetailsService>();
builder.Services.AddScoped<IGetProductImageService, GetProductImageService>();
builder.Services.AddScoped<ICartService, CartService>();

var minioOptions = builder.Configuration
    .GetRequiredSection(MinioOptions.SectionName)
    .Get<MinioOptions>() ?? throw new InvalidOperationException("پیکربندی MinIO یافت نشد.");
if (string.IsNullOrWhiteSpace(minioOptions.Endpoint) ||
    string.IsNullOrWhiteSpace(minioOptions.AccessKey) ||
    string.IsNullOrWhiteSpace(minioOptions.SecretKey) ||
    string.IsNullOrWhiteSpace(minioOptions.BucketName))
{
    throw new InvalidOperationException("مقادیر Endpoint، AccessKey، SecretKey و BucketName برای MinIO الزامی هستند.");
}

builder.Services.AddSingleton(minioOptions);
builder.Services.AddSingleton<IMinioClient>(_ =>
{
    var client = new MinioClient()
        .WithEndpoint(minioOptions.Endpoint)
        .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey);
    if (minioOptions.UseSSL) client = client.WithSSL();
    return client.Build();
});
builder.Services.AddSingleton<IFileStorageService, MinioFileStorageService>();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
