using System.Security.Claims;
using EndPoint.Site.Services;
using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Application.Services.Orders;
using FinalProject_Store.Domain.Entities.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class CookieAndWorkerRegressionTests
{
    private static ServiceProvider CookieServices()
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        return services.BuildServiceProvider();
    }
    private static CookieValidatePrincipalContext Validation(ServiceProvider provider, string id, params string[] roles)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id) }
            .Concat(roles.Select(x => new Claim(ClaimTypes.Role, x))), "test");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), new AuthenticationProperties(), CookieAuthenticationDefaults.AuthenticationScheme);
        return new(new DefaultHttpContext { RequestServices = provider }, new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler)), new CookieAuthenticationOptions(), ticket);
    }

    [Theory]
    [InlineData("invalid", true)] [InlineData("999", true)] [InlineData("1", false)]
    public async Task InvalidOrDisabledCookieIsRejectedAndSignedOut(string userId, bool active)
    {
        var f = new ServiceFixture(); f.User.isActive = active;
        using var provider = CookieServices(); var validation = Validation(provider, userId, "Admin");
        await new StoreCookieEvents(f.Db).ValidatePrincipal(validation);
        Assert.Null(validation.Principal); Assert.Contains(".AspNetCore.Cookies=", validation.HttpContext.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task CookieDropsRevokedAdminRoleAndRenewsWithCurrentRoles()
    {
        var f = new ServiceFixture(); var role = new Role { Id = 3, Name = "Customer" };
        f.Db.Roles.Add(role); f.Db.UserInRoles.Add(new UserInRole { UserId = 1, RoleId = 3, Role = role });
        using var provider = CookieServices(); var validation = Validation(provider, "1", "Admin");
        await new StoreCookieEvents(f.Db).ValidatePrincipal(validation);
        Assert.False(validation.Principal!.IsInRole("Admin")); Assert.True(validation.Principal.IsInRole("Customer")); Assert.True(validation.ShouldRenew);
        var current = Validation(provider, "1", "Customer"); await new StoreCookieEvents(f.Db).ValidatePrincipal(current);
        Assert.False(current.ShouldRenew);
    }

    [Fact]
    public async Task CookieWithNoRemainingRolesLosesAdminAccess()
    {
        var f = new ServiceFixture(); using var provider = CookieServices(); var validation = Validation(provider, "1", "Admin");
        await new StoreCookieEvents(f.Db).ValidatePrincipal(validation);
        Assert.Empty(validation.Principal!.FindAll(ClaimTypes.Role)); Assert.True(validation.ShouldRenew);
    }

    [Fact]
    public async Task ExpirationWorkerRestoresExpiredRemovedOrderOnInitialSweep()
    {
        var f = new ServiceFixture(); var p = await f.StartPayment();
        p.Order.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1); p.Order.IsRemoved = true;
        var services = new ServiceCollection();
        services.AddScoped<IDataBaseContext>(_ => f.Db); services.AddScoped<IOrderLifecycleService>(_ => f.Lifecycle);
        using var provider = services.BuildServiceProvider();
        using var worker = new OrderExpirationWorker(provider.GetRequiredService<IServiceScopeFactory>(), new ConfigurationBuilder().Build(), NullLogger<OrderExpirationWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try { Assert.Equal(5, f.Product.Inventory); Assert.True(p.Order.ReservationExpired); }
        finally { await worker.StopAsync(CancellationToken.None); }
        Assert.False(f.Lifecycle.Expire(p.OrderId)); Assert.Equal(5, f.Product.Inventory);
    }

    [Fact]
    public async Task ExpirationWorkerLeavesUnexpiredReservationAlone()
    {
        var f = new ServiceFixture(); f.CreateOrder();
        var services = new ServiceCollection(); services.AddScoped<IDataBaseContext>(_ => f.Db); services.AddScoped<IOrderLifecycleService>(_ => f.Lifecycle);
        using var provider = services.BuildServiceProvider();
        using var worker = new OrderExpirationWorker(provider.GetRequiredService<IServiceScopeFactory>(), new ConfigurationBuilder().Build(), NullLogger<OrderExpirationWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try { Assert.Equal(3, f.Product.Inventory); Assert.False(f.Db.Orders.Single().ReservationExpired); }
        finally { await worker.StopAsync(CancellationToken.None); }
    }
}
