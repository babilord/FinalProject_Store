using System.Text.Json;
using FinalProject_Store.Application.Common.Security;
using FinalProject_Store.Domain.Entities.Users;
using FinalProject_Store.Domain.Entities.Products;
using FinalProject_Store.Persistence.Contexts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

const string catalog = "FinalProject_Store_BrowserTests";
const string owner = "Store.Browser.v1";
var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STORE_BROWSER_SQL")
    ?? "Server=DESKTOP-B71VTVJ;Trusted_Connection=True;TrustServerCertificate=True");
connection.InitialCatalog = catalog; // Never use the supplied application's catalog.
connection.ConnectTimeout = 10;
if (args[0] == "init")
{
    var masterConnection = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" };
    using var master = new SqlConnection(masterConnection.ConnectionString);
    master.Open();
    using var exists = master.CreateCommand();
    exists.CommandText = "SELECT DB_ID(@name)";
    exists.Parameters.AddWithValue("@name", catalog);
    if (exists.ExecuteScalar() is DBNull)
    {
        using var create = master.CreateCommand();
        create.CommandText = "CREATE DATABASE [FinalProject_Store_BrowserTests]";
        create.ExecuteNonQuery();
        using var owned = new SqlConnection(connection.ConnectionString);
        owned.Open();
        using var mark = owned.CreateCommand();
        mark.CommandText = "EXEC sys.sp_addextendedproperty @name=N'StoreBrowserOwner', @value=N'Store.Browser.v1'";
        mark.ExecuteNonQuery();
    }
}
using (var test = new SqlConnection(connection.ConnectionString))
{
    test.Open();
    using var guard = test.CreateCommand();
    guard.CommandText = "SELECT CONVERT(nvarchar(128), value) FROM sys.extended_properties WHERE class=0 AND name=N'StoreBrowserOwner'";
    if ((string?)guard.ExecuteScalar() != owner) throw new InvalidOperationException("Unowned browser database; refusing writes.");
}
using var db = new DataBaseContext(new DbContextOptionsBuilder<DataBaseContext>().UseSqlServer(connection.ConnectionString).Options);
if (args[0] == "init")
{
    db.Database.Migrate();
    var run = Guid.NewGuid().ToString("N");
    var password = "BrowserTest!123";
    var users = new[] { ("admin", 1L), ("operator", 2L), ("customer", 3L) }.Select(role => new User
    {
        FullName = "آزمایش " + role.Item1, Email = role.Item1 + "." + run + "@browser.test",
        Password = new PasswordHasher().HashPassword(password), isActive = true,
        UserInRoles = new List<UserInRole> { new() { RoleId = role.Item2 } }
    }).ToArray();
    db.Users.AddRange(users);
    var paginationKey = "paged." + run;
    db.Users.AddRange(Enumerable.Range(1, 21).Select(index => new User {
        FullName = "کاربر صفحه " + index, Email = paginationKey + "." + index + "@browser.test",
        Password = users[2].Password, isActive = true,
        UserInRoles = new List<UserInRole> { new() { RoleId = 3 } }
    }));
    var category = new Category { Name = "دسته آزمایشی " + run };
    var product = new Product { Name = "محصول آزمایشی " + run, Brand = "Browser", Description = "توضیحات فارسی محصول", Price = 12500, Inventory = 20, ImageSrc = "", Category = category, IsActive = true };
    db.Products.Add(product);
    db.SaveChanges();
    Console.WriteLine(JsonSerializer.Serialize(new { run, password, paginationKey, admin = users[0].Email, operatorEmail = users[1].Email, customer = users[2].Email, productId = product.Id, productName = product.Name, categoryId = category.Id, connection = connection.ConnectionString }));
}
else if (args[0] == "state")
{
    var id = long.Parse(args[1]);
    Console.WriteLine(JsonSerializer.Serialize(new { inventory = db.Products.IgnoreQueryFilters().Single(p => p.Id == id).Inventory,
        orders = db.Orders.IgnoreQueryFilters().Where(o => o.Items.Any(i => i.ProductId == id)).Select(o => new { id = o.Id, status = o.Status.ToString(), expired = o.ReservationExpired }).ToArray() }));
}
else if (args[0] == "expire")
{
    var order = db.Orders.Single(o => o.Id == long.Parse(args[1]));
    if (order.Status != FinalProject_Store.Domain.Entities.Orders.OrderStatus.PendingPayment) throw new InvalidOperationException("Only pending test reservations may expire.");
    order.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
    db.SaveChanges();
}
else throw new ArgumentException("Expected init, state or expire.");
