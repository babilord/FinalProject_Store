using System.Reflection;
using System.Security.Claims;
using EndPoint.Site.Controllers;
using FinalProject_Store.Domain.Entities.Payments;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Xunit;
using AdminOrders = EndPoint.Site.Areas.Admin.Controllers.OrdersController;
using AdminPayments = EndPoint.Site.Areas.Admin.Controllers.PaymentsController;

public class AuthorizationRegressionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AntiforgeryRejectsMissingOrForgedTokens(bool forged)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddAntiforgery();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider }; context.Request.Method = "POST";
        if (forged) { context.Request.Headers.Cookie = ".AspNetCore.Antiforgery=forged"; context.Request.Headers["RequestVerificationToken"] = "forged"; }
        await Assert.ThrowsAsync<AntiforgeryValidationException>(() => provider.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context));
    }
    public static IEnumerable<object[]> ProtectedActions()
    {
        foreach (var type in new[] { typeof(AdminOrders), typeof(AdminPayments), typeof(CartController), typeof(OrdersController), typeof(PaymentsController), typeof(FakePaymentsController) })
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.GetCustomAttribute<AllowAnonymousAttribute>() != null) continue;
            foreach (var role in new[] { "Anonymous", "Customer", "Operator", "Admin" })
                yield return new object[] { type, method.Name, method.GetParameters().Length, role };
        }
    }

    [Theory, MemberData(nameof(ProtectedActions))]
    public async Task ActualAuthorizationMiddlewareGuardsCustomerAndAdminActions(Type controller, string action, int parameterCount, string role)
    {
        var method = controller.GetMethods().Single(x => x.Name == action && x.GetParameters().Length == parameterCount);
        var metadata = controller.GetCustomAttributes<AuthorizeAttribute>(true).Cast<object>()
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>(true)).ToArray();
        Assert.NotEmpty(metadata);
        var services = new ServiceCollection(); services.AddLogging(); services.AddRouting(); services.AddAuthorization();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options => {
            options.LoginPath = "/Authentication/Login"; options.AccessDeniedPath = "/Authentication/AccessDenied";
        });
        using var provider = services.BuildServiceProvider();
        var builder = new ApplicationBuilder(provider); builder.UseAuthorization();
        var reached = false; builder.Run(ctx => { reached = true; ctx.Response.StatusCode = 200; return Task.CompletedTask; });
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = "/protected"; context.Request.Host = new HostString("localhost"); context.Request.Scheme = "https";
        if (role != "Anonymous") context.User = new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, role) }, "test"));
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "regression"));
        await builder.Build()(context);
        var allowed = role != "Anonymous" && (!controller.Namespace!.Contains(".Admin.") || role == "Admin");
        Assert.Equal(allowed, reached);
        Assert.Equal(allowed ? 200 : 302, context.Response.StatusCode);
        if (!allowed) Assert.Contains(role == "Anonymous" ? "/Authentication/Login" : "/Authentication/AccessDenied", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public void CustomerAndAdminMutationActionsRequirePostAndAntiforgery()
    {
        var count = 0;
        foreach (var type in new[] { typeof(CartController), typeof(OrdersController), typeof(PaymentsController), typeof(FakePaymentsController), typeof(AdminOrders) })
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.GetCustomAttribute<HttpPostAttribute>() == null) continue;
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()); count++;
        }
        Assert.Equal(8, count);
        var callback = typeof(PaymentsController).GetMethod(nameof(PaymentsController.Callback))!;
        Assert.NotNull(callback.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(callback.GetCustomAttribute<HttpGetAttribute>());
        Assert.True(callback.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
    }

    [Theory]
    [InlineData("Development", 2, PaymentStatus.Succeeded, 404)]
    [InlineData("Production", 1, PaymentStatus.Succeeded, 404)]
    [InlineData("Development", 1, PaymentStatus.Pending, 400)]
    [InlineData("Development", 1, PaymentStatus.Succeeded, 302)]
    public async Task SimulatorEnforcesOwnershipEnvironmentAndOutcomeWithoutChangingState(string environment, long user, PaymentStatus outcome, int expected)
    {
        var f = new ServiceFixture(); var payment = await f.StartPayment();
        var controller = new FakePaymentsController(f.Db, f.Gateway, new WebEnvironment { EnvironmentName = environment });
        var context = new DefaultHttpContext { User = new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()) }, "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        var result = controller.Complete(payment.Token, outcome);
        var status = result switch { NotFoundResult => 404, BadRequestResult => 400, RedirectToActionResult => 302, _ => 0 };
        Assert.Equal(expected, status); Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(FinalProject_Store.Domain.Entities.Orders.OrderStatus.PendingPayment, payment.Order.Status);
        Assert.Equal(3, f.Product.Inventory);
    }

    private sealed class WebEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Regression";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = ".";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
