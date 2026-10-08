using FinalProject_Store.Application.Services.Carts;
using FinalProject_Store.Application.Services.Orders;
using FinalProject_Store.Application.Services.Payments;
using FinalProject_Store.Domain.Entities.Carts;
using FinalProject_Store.Domain.Entities.Orders;
using FinalProject_Store.Domain.Entities.Payments;
using FinalProject_Store.Domain.Entities.Products;
using FinalProject_Store.Domain.Entities.Users;
using FinalProject_Store.Infrastructures.Payments;
using FinalProject_Store.Persistence.Contexts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

public class ExistingRegressionTests
{
[Xunit.Fact]
public async Task ExistingLifecycleChecks()
{
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
var gateway = new FakePaymentGateway(new EphemeralDataProtectionProvider(), new TestEnvironment());
(TestContext Context, Product Product, Order Order, PaymentService Payments, OrderLifecycleService Lifecycle) Fixture()
{
    var db = new TestContext();
    var user = new User { Id = 1, isActive = true };
    db.Users.Add(user);
    var product = new Product { Id = 1, Name = "Snapshot", Inventory = 1, Price = 10, IsActive = true };
    db.Products.Add(product);
    var order = new Order { Id = 1, UserId = 1, User = user, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30), Total = 40 };
    order.Items.Add(new OrderItem { ProductId = 1, ProductName = "Snapshot", Quantity = 4, UnitPrice = 10, LineTotal = 40 });
    db.Orders.Add(order);
    return (db, product, order, new PaymentService(db, gateway, NullLogger<PaymentService>.Instance),
        new OrderLifecycleService(db, NullLogger<OrderLifecycleService>.Instance));
}
string Receipt(Payment payment, PaymentStatus outcome) => gateway.IssueReceipt(payment.Token, payment.OrderId, payment.Amount, payment.ExpiresAtUtc, outcome);

Check((int)OrderStatus.PendingPayment == 1 && (int)OrderStatus.Paid == 2 && (int)OrderStatus.Cancelled == 3, "stored status values preserved");
foreach (var from in Enum.GetValues<OrderStatus>())
foreach (var to in Enum.GetValues<OrderStatus>())
{
    var expected = (from == OrderStatus.Paid && to == OrderStatus.Processing) ||
        (from == OrderStatus.Processing && to == OrderStatus.Shipped) || (from == OrderStatus.Shipped && to == OrderStatus.Delivered);
    if (OrderRules.CanAdvance(from, to) != expected) throw new Exception("Invalid fulfillment transition");
}
Check(true, "all 36 fulfillment state pairs checked");

var f = Fixture();
Check(!(await f.Payments.PayAsync(2, 1)).IsSuccess, "payment ownership enforced");
Check(!f.Lifecycle.Cancel(2, 1).IsSuccess && f.Product.Inventory == 1, "cancellation ownership enforced");
Check(!f.Lifecycle.Expire(1) && f.Product.Inventory == 1, "unexpired reservation retained");
Check((await f.Payments.PayAsync(1, 1)).IsSuccess, "valid payment requested");
var attempt = f.Context.Payments.Single();
await f.Payments.PayAsync(1, 1);
Check(f.Context.Payments.Count() == 1 && attempt.ExpiresAtUtc <= f.Order.ExpiresAtUtc, "duplicate payment requests reuse attempt and respect order deadline");
Check(!(await f.Payments.CallbackAsync(attempt.Token, "forged")).IsSuccess && attempt.Status == PaymentStatus.Pending, "forged receipt cannot consume pending attempt");
var wrongAmount = gateway.IssueReceipt(attempt.Token, 1, 999, attempt.ExpiresAtUtc, PaymentStatus.Succeeded);
Check(!(await f.Payments.CallbackAsync(attempt.Token, wrongAmount)).IsSuccess, "receipt amount mismatch rejected");
await f.Payments.CallbackAsync(attempt.Token, Receipt(attempt, PaymentStatus.Failed));
Check(f.Order.Status == OrderStatus.PendingPayment && f.Product.Inventory == 1, "failed attempt preserves reservation");
await f.Payments.PayAsync(1, 1);
attempt = f.Context.Payments.OrderBy(x => x.Id).Last();
await f.Payments.CallbackAsync(attempt.Token, Receipt(attempt, PaymentStatus.Cancelled));
Check(f.Order.Status == OrderStatus.PendingPayment && f.Product.Inventory == 1, "cancelled attempt preserves reservation");
await f.Payments.PayAsync(1, 1);
attempt = f.Context.Payments.OrderBy(x => x.Id).Last();
var receipt = Receipt(attempt, PaymentStatus.Succeeded);
await f.Payments.CallbackAsync(attempt.Token, receipt);
await f.Payments.CallbackAsync(attempt.Token, receipt);
Check(f.Order.Status == OrderStatus.Paid && f.Product.Inventory == 1 && attempt.ReferenceId != null, "success and replay never decrement stock again");
Check(!(await f.Payments.CallbackAsync(attempt.Token, "forged")).IsSuccess, "completed attempts still reject forged callbacks");
f.Order.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
Check(!f.Lifecycle.Expire(1) && !f.Lifecycle.Cancel(1, 1).IsSuccess && f.Product.Inventory == 1, "paid order never expires or cancels");
Check(!(await f.Payments.PayAsync(1, 1)).IsSuccess, "paid order cannot pay again");
Check(!f.Lifecycle.Advance(1, OrderStatus.Delivered, 99).IsSuccess, "fulfillment cannot skip processing/shipping");
Check(f.Lifecycle.Advance(1, OrderStatus.Processing, 99).IsSuccess && f.Lifecycle.Advance(1, OrderStatus.Shipped, 99).IsSuccess &&
    f.Lifecycle.Advance(1, OrderStatus.Delivered, 99).IsSuccess, "valid fulfillment progression succeeds");

f = Fixture();
await f.Payments.PayAsync(1, 1);
attempt = f.Context.Payments.Single();
receipt = Receipt(attempt, PaymentStatus.Succeeded);
f.Order.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
Check(!(await f.Payments.CallbackAsync(attempt.Token, receipt)).IsSuccess, "expired order rejects otherwise valid success receipt");
Check(!(await f.Payments.PayAsync(1, 1)).IsSuccess, "expired order rejects retry");
Check(f.Lifecycle.Expire(1) && f.Order.Status == OrderStatus.Cancelled && f.Order.ReservationExpired && f.Product.Inventory == 5, "expiration restores exact reservation");
f.Lifecycle.Expire(1); f.Lifecycle.Cancel(1, 1); await f.Payments.CallbackAsync(attempt.Token, receipt);
Check(f.Product.Inventory == 5 && f.Order.Status == OrderStatus.Cancelled && attempt.Status == PaymentStatus.Cancelled, "repeated cleanup, cancel, and callback never restore twice");

f = Fixture();
await f.Payments.PayAsync(1, 1);
attempt = f.Context.Payments.Single();
f.Context.Users.Single().isActive = false;
Check(!(await f.Payments.CallbackAsync(attempt.Token, Receipt(attempt, PaymentStatus.Succeeded))).IsSuccess &&
    f.Order.Status == OrderStatus.PendingPayment, "disabled account cannot complete pending payment");

f = Fixture();
f.Product.IsRemoved = true;
Check(f.Lifecycle.Cancel(1, 1).IsSuccess && f.Product.Inventory == 5 && !f.Order.ReservationExpired, "customer cancellation restores soft-deleted product");
Check(!(await f.Payments.PayAsync(1, 1)).IsSuccess, "cancelled order cannot pay");

f = Fixture();
f.Product.Inventory = 5;
var cart = new Cart { UserId = 1 };
var cartItem = new CartItem { Product = f.Product, ProductId = 1, Quantity = 4, Cart = cart };
cart.Items.Add(cartItem); f.Context.CartItems.Add(cartItem); f.Context.Carts.Add(cart);
var orders = new OrderService(f.Context, NullLogger<OrderService>.Instance);
var input = new CreateOrderDto { FullName = "Test", MobileNumber = "09123456789", Province = "Test", City = "Test", PostalAddress = "Test address 123", PostalCode = "1234567890" };
Check(!orders.Create(1, new CreateOrderDto()).IsSuccess && f.Product.Inventory == 5, "shipping validation enforced by service");
f.Product.IsActive = false;
Check(!orders.Create(1, input).IsSuccess && f.Product.Inventory == 5, "inactive product rejected at checkout");
f.Product.IsActive = true;
Check(orders.Create(1, input).IsSuccess && f.Product.Inventory == 1, "checkout reserves stock once");
Check(!orders.Create(1, input).IsSuccess && f.Product.Inventory == 1, "repeat checkout from emptied cart creates no second order");
cart.Items.Add(cartItem); f.Context.CartItems.Add(cartItem);
var carts = new CartService(f.Context);
Check(!carts.Add(1, 1, int.MaxValue).IsSuccess && cartItem.Quantity == 4, "cart quantity addition cannot overflow");

using (var modelContext = new DataBaseContext(new DbContextOptionsBuilder<DataBaseContext>()
    .UseSqlServer("Server=unused;Database=unused;Integrated Security=True;TrustServerCertificate=True").Options))
{
    var model = modelContext.Model;
    Check(model.FindEntityType(typeof(Product))!.FindProperty(nameof(Product.RowVersion))!.IsConcurrencyToken, "product rowversion concurrency mapping present");
    Check(model.FindEntityType(typeof(Order))!.GetIndexes().Any(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "Status", "ExpiresAtUtc" })), "expiration scan index mapped");
    Check(model.FindEntityType(typeof(Payment))!.GetIndexes().Count(x => x.IsUnique && x.GetFilter() != null) == 2, "unique pending/successful payment guards preserved");
}
Console.WriteLine($"{passed} legacy checks passed. No database was opened or migrated. See README.md for verification limits.");

}
}

internal sealed class TestEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "Store.Regression";
    public string ContentRootPath { get; set; } = ".";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
