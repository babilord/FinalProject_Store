using System.Text.Json;
using FinalProject_Store.Domain.Entities.Carts;
using FinalProject_Store.Domain.Entities.Products;
using FinalProject_Store.Domain.Entities.Users;
using FinalProject_Store.Persistence.Contexts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

public sealed class SqlFixture : IAsyncLifetime
{
    public const string DatabaseName = "FinalProject_Store_IntegrationTests";
    private const string Ownership = "Store.SqlIntegration.v1";
    private string connection = "";

    public async Task InitializeAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "EndPoint.Site", "appsettings.json")))
            directory = directory.Parent;
        if (directory == null) throw new InvalidOperationException("Cannot locate repository configuration.");
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "EndPoint.Site", "appsettings.json")));
        var configured = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STORE_SQL_INTEGRATION_CONNECTION") ?? configured);
        // Derive a dedicated catalog; never connect to the configured application catalog.
        builder.InitialCatalog = DatabaseName;
        builder.ConnectTimeout = 10;
        connection = builder.ConnectionString;
        builder.InitialCatalog = "master";
        using var master = new SqlConnection(builder.ConnectionString);
        await master.OpenAsync();
        using var exists = master.CreateCommand();
        exists.CommandText = "SELECT DB_ID(@name)";
        exists.Parameters.AddWithValue("@name", DatabaseName);
        if (await exists.ExecuteScalarAsync() is DBNull)
        {
            using var create = master.CreateCommand();
            create.CommandText = "CREATE DATABASE [FinalProject_Store_IntegrationTests]";
            await create.ExecuteNonQueryAsync();
            using var owned = new SqlConnection(connection);
            await owned.OpenAsync();
            using var mark = owned.CreateCommand();
            mark.CommandText = "EXEC sys.sp_addextendedproperty @name=N'StoreIntegrationOwner', @value=N'Store.SqlIntegration.v1'";
            await mark.ExecuteNonQueryAsync();
        }
        using (var test = new SqlConnection(connection))
        {
            await test.OpenAsync();
            using var guard = test.CreateCommand();
            guard.CommandText = "SELECT CONVERT(nvarchar(128), value) FROM sys.extended_properties WHERE class=0 AND name=N'StoreIntegrationOwner'";
            if ((string?)await guard.ExecuteScalarAsync() != Ownership)
                throw new InvalidOperationException("Refusing to migrate an existing database without the test ownership marker.");
        }
        using var db = Open();
        await db.Database.MigrateAsync();
    }

    public DataBaseContext Open(params IInterceptor[] interceptors)
    {
        if (new SqlConnectionStringBuilder(connection).InitialCatalog != DatabaseName)
            throw new InvalidOperationException("Unsafe integration database target.");
        return new(new DbContextOptionsBuilder<DataBaseContext>().UseSqlServer(connection)
            .AddInterceptors(interceptors).Options);
    }
    public Task DisposeAsync() => Task.CompletedTask; // Keep the owned schema for subsequent runs.

    public Scenario Seed(int stock = 1, int customers = 1) => new(this, stock, customers);
}

public sealed class Scenario : IDisposable
{
    private readonly SqlFixture fixture;
    public long ProductId { get; }
    public long CategoryId { get; }
    public long[] Users { get; }
    public Scenario(SqlFixture fixture, int stock, int customers)
    {
        this.fixture = fixture;
        using var db = fixture.Open();
        var category = new Category { Name = "integration-" + Guid.NewGuid() };
        var product = new Product { Name = "Integration product", Brand = "Test", Description = "Test", ImageSrc = "", Price = 12.50m, Inventory = stock, Category = category };
        db.Products.Add(product);
        var users = Enumerable.Range(0, customers).Select(_ => new User { FullName = "Integration customer", Email = Guid.NewGuid() + "@integration.test", Password = "unused", isActive = true }).ToArray();
        foreach (var user in users)
            db.Carts.Add(new Cart { User = user, Items = new List<CartItem> { new() { Product = product, Quantity = 1 } } });
        db.SaveChanges();
        ProductId = product.Id; CategoryId = category.Id; Users = users.Select(x => x.Id).ToArray();
    }
    public void Dispose()
    {
        using var db = fixture.Open();
        using var transaction = db.Database.BeginTransaction();
        var orders = db.Orders.IgnoreQueryFilters().Where(x => Users.Contains(x.UserId)).Select(x => x.Id);
        db.Payments.IgnoreQueryFilters().Where(x => orders.Contains(x.OrderId)).ExecuteDelete();
        db.OrderItems.IgnoreQueryFilters().Where(x => orders.Contains(x.OrderId)).ExecuteDelete();
        db.Orders.IgnoreQueryFilters().Where(x => Users.Contains(x.UserId)).ExecuteDelete();
        var carts = db.Carts.IgnoreQueryFilters().Where(x => Users.Contains(x.UserId)).Select(x => x.Id);
        db.CartItems.IgnoreQueryFilters().Where(x => carts.Contains(x.CartId)).ExecuteDelete();
        db.Carts.IgnoreQueryFilters().Where(x => Users.Contains(x.UserId)).ExecuteDelete();
        db.Users.IgnoreQueryFilters().Where(x => Users.Contains(x.Id)).ExecuteDelete();
        db.Products.IgnoreQueryFilters().Where(x => x.Id == ProductId).ExecuteDelete();
        db.Categories.IgnoreQueryFilters().Where(x => x.Id == CategoryId).ExecuteDelete();
        transaction.Commit();
    }
}
