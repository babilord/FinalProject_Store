using FinalProject_Store.Application.Services.Orders;
using FinalProject_Store.Application.Services.Payments;
using FinalProject_Store.Domain.Entities.Orders;
using FinalProject_Store.Domain.Entities.Payments;
using FinalProject_Store.Infrastructures.Payments;
using FinalProject_Store.Persistence.Contexts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class SqlIntegrationTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    private readonly FakePaymentGateway gateway = new(new EphemeralDataProtectionProvider(), new DevelopmentEnvironment());
    private static OrderService Orders(DataBaseContext db) => new(db, NullLogger<OrderService>.Instance);
    private static OrderLifecycleService Lifecycle(DataBaseContext db) => new(db, NullLogger<OrderLifecycleService>.Instance);
    private PaymentService Payments(DataBaseContext db) => new(db, gateway, NullLogger<PaymentService>.Instance);
    private static CreateOrderDto Input() => new() { FullName = "Integration customer", MobileNumber = "09123456789", Province = "Province", City = "City", PostalAddress = "Integration address", PostalCode = "1234567890" };
    private long Checkout(Scenario scenario)
    {
        using var db = fixture.Open();
        var result = Orders(db).Create(scenario.Users[0], Input());
        Assert.True(result.IsSuccess, result.Message);
        return result.Data;
    }
    private async Task<Payment> Start(Scenario scenario, long order)
    {
        using var db = fixture.Open();
        var result = await Payments(db).PayAsync(scenario.Users[0], order);
        Assert.True(result.IsSuccess, result.Message);
        return db.Payments.AsNoTracking().Single(x => x.OrderId == order && x.Status == PaymentStatus.Pending);
    }
    private string Receipt(Payment p, PaymentStatus status) => gateway.IssueReceipt(p.Token, p.OrderId, p.Amount, p.ExpiresAtUtc, status);

    [Fact]
    public async Task ConcurrentCheckoutCompetesForLastInventory()
    {
        using var scenario = fixture.Seed(customers: 2);
        using var barrier = new Barrier(2);
        var interceptor = new CheckoutBarrier(barrier);
        var results = await Task.WhenAll(scenario.Users.Select(user => Task.Run(() =>
        {
            using var db = fixture.Open(interceptor);
            return Orders(db).Create(user, Input());
        })));
        Assert.Single(results.Where(x => x.IsSuccess));
        Assert.Single(results.Where(x => !x.IsSuccess));
        using var read = fixture.Open();
        Assert.Equal(0, read.Products.Single(x => x.Id == scenario.ProductId).Inventory);
        Assert.Single(read.Orders.Where(x => scenario.Users.Contains(x.UserId)));
        Assert.Single(read.OrderItems.Where(x => x.ProductId == scenario.ProductId));
        Assert.Single(read.CartItems.Where(x => x.ProductId == scenario.ProductId));
    }

    [Fact]
    public void OrderFailureRollsBackAlreadyWrittenStockOrderAndCart()
    {
        using var scenario = fixture.Seed();
        var fault = new FailAfterSave();
        using (var db = fixture.Open(fault))
            Assert.False(Orders(db).Create(scenario.Users[0], Input()).IsSuccess);
        Assert.True(fault.Triggered); // Failure occurs after actual SQL writes, before commit.
        using var read = fixture.Open();
        Assert.Equal(1, read.Products.Single(x => x.Id == scenario.ProductId).Inventory);
        Assert.Empty(read.Orders.Where(x => x.UserId == scenario.Users[0]));
        Assert.Empty(read.OrderItems.Where(x => x.ProductId == scenario.ProductId));
        Assert.Single(read.CartItems.Where(x => x.ProductId == scenario.ProductId));
        Assert.True(Orders(read).Create(scenario.Users[0], Input()).IsSuccess);
    }

    [Fact]
    public async Task ConcurrentExpirationRestoresInventoryExactlyOnce()
    {
        using var scenario = fixture.Seed();
        var order = Checkout(scenario);
        var payment = await Start(scenario, order);
        using (var db = fixture.Open())
        {
            Assert.False(Lifecycle(db).Expire(order));
            db.Orders.Single(x => x.Id == order).ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            db.SaveChanges();
        }
        var results = await Together(4, () => { using var db = fixture.Open(); return Task.FromResult(Lifecycle(db).Expire(order)); });
        Assert.Single(results.Where(x => x));
        using (var db = fixture.Open())
        {
            Assert.False(Lifecycle(db).Expire(order));
            Assert.True((await Payments(db).CallbackAsync(payment.Token, Receipt(payment, PaymentStatus.Succeeded))).IsSuccess);
        }
        using var read = fixture.Open();
        Assert.Equal(1, read.Products.Single(x => x.Id == scenario.ProductId).Inventory);
        var saved = read.Orders.Single(x => x.Id == order);
        Assert.Equal(OrderStatus.Cancelled, saved.Status); Assert.True(saved.ReservationExpired);
        Assert.Equal(PaymentStatus.Cancelled, read.Payments.Single(x => x.Id == payment.Id).Status);
    }

    [Fact]
    public async Task CustomerCancellationRestoresInventoryAndRejectsOtherCustomer()
    {
        using var scenario = fixture.Seed(stock: 2, customers: 2);
        var order = Checkout(scenario);
        var payment = await Start(scenario, order);
        using (var db = fixture.Open()) Assert.False(Lifecycle(db).Cancel(scenario.Users[1], order).IsSuccess);
        var results = await Together(4, () => { using var db = fixture.Open(); return Task.FromResult(Lifecycle(db).Cancel(scenario.Users[0], order).IsSuccess); });
        Assert.All(results, Assert.True);
        using (var db = fixture.Open()) Assert.True((await Payments(db).CallbackAsync(payment.Token, Receipt(payment, PaymentStatus.Succeeded))).IsSuccess);
        using var read = fixture.Open();
        Assert.Equal(2, read.Products.Single(x => x.Id == scenario.ProductId).Inventory);
        var saved = read.Orders.Single(x => x.Id == order);
        Assert.Equal(OrderStatus.Cancelled, saved.Status); Assert.False(saved.ReservationExpired);
        Assert.Equal(PaymentStatus.Cancelled, read.Payments.Single(x => x.Id == payment.Id).Status);
    }

    [Fact]
    public async Task DuplicateConcurrentCallbacksPersistOneSuccessfulPayment()
    {
        using var scenario = fixture.Seed();
        var order = Checkout(scenario);
        var payment = await Start(scenario, order);
        var receipt = Receipt(payment, PaymentStatus.Succeeded);
        var results = await Together(4, async () => { using var db = fixture.Open(); return (await Payments(db).CallbackAsync(payment.Token, receipt)).IsSuccess; });
        Assert.All(results, Assert.True);
        using (var db = fixture.Open()) Assert.True((await Payments(db).CallbackAsync(payment.Token, Receipt(payment, PaymentStatus.Failed))).IsSuccess);
        using var read = fixture.Open();
        var saved = Assert.Single(read.Payments.Where(x => x.OrderId == order));
        Assert.Equal(PaymentStatus.Succeeded, saved.Status); Assert.Equal("FAKE-" + payment.Token, saved.ReferenceId);
        Assert.Equal(OrderStatus.Paid, read.Orders.Single(x => x.Id == order).Status);
        Assert.Equal(0, read.Products.Single(x => x.Id == scenario.ProductId).Inventory);
    }

    [Fact]
    public async Task ConcurrentPaymentRequestsAndRetriesReuseOnePendingAttempt()
    {
        using var scenario = fixture.Seed();
        var order = Checkout(scenario);
        async Task<string[]> Requests() => await Together(4, async () =>
        {
            using var db = fixture.Open();
            var result = await Payments(db).PayAsync(scenario.Users[0], order);
            Assert.True(result.IsSuccess, result.Message); return result.Data;
        });
        Assert.Single((await Requests()).Distinct());
        Payment first;
        using (var read = fixture.Open()) first = read.Payments.AsNoTracking().Single(x => x.OrderId == order);
        using (var db = fixture.Open()) Assert.True((await Payments(db).CallbackAsync(first.Token, Receipt(first, PaymentStatus.Failed))).IsSuccess);
        Assert.Single((await Requests()).Distinct());
        Payment retry;
        using (var read = fixture.Open())
        {
            Assert.Equal(2, read.Payments.Count(x => x.OrderId == order));
            retry = read.Payments.AsNoTracking().Single(x => x.OrderId == order && x.Status == PaymentStatus.Pending);
            Assert.NotEqual(first.Token, retry.Token);
            // An expired attempt must also be replaced atomically under concurrent requests.
            read.Payments.Single(x => x.Id == retry.Id).ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            read.SaveChanges();
        }
        Assert.Single((await Requests()).Distinct());
        using var final = fixture.Open();
        Assert.Equal(3, final.Payments.Count(x => x.OrderId == order));
        Assert.Single(final.Payments.Where(x => x.OrderId == order && x.Status == PaymentStatus.Pending));
        Assert.Equal(PaymentStatus.Failed, final.Payments.Single(x => x.Id == first.Id).Status);
        Assert.Equal(PaymentStatus.Cancelled, final.Payments.Single(x => x.Id == retry.Id).Status);
        Assert.Equal(0, final.Products.Single(x => x.Id == scenario.ProductId).Inventory);
    }

    [Fact]
    public async Task OrderTransitionsPersistAndIllegalTransitionsLeaveStateUnchanged()
    {
        using var scenario = fixture.Seed();
        var order = Checkout(scenario);
        using (var db = fixture.Open()) Assert.False(Lifecycle(db).Advance(order, OrderStatus.Processing, scenario.Users[0]).IsSuccess);
        var payment = await Start(scenario, order);
        using (var db = fixture.Open()) Assert.True((await Payments(db).CallbackAsync(payment.Token, Receipt(payment, PaymentStatus.Succeeded))).IsSuccess);
        foreach (var target in new[] { OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered })
        {
            using (var db = fixture.Open()) Assert.True(Lifecycle(db).Advance(order, target, scenario.Users[0]).IsSuccess);
            using var read = fixture.Open();
            Assert.Equal(target, read.Orders.Single(x => x.Id == order).Status);
            Assert.Equal(target, Orders(read).GetDetails(scenario.Users[0], order).Data.Status);
        }
        using (var db = fixture.Open())
        {
            Assert.False(Lifecycle(db).Advance(order, OrderStatus.Paid, scenario.Users[0]).IsSuccess);
            Assert.False(Lifecycle(db).Cancel(scenario.Users[0], order).IsSuccess);
            Assert.False(Lifecycle(db).Expire(order));
        }
        using var final = fixture.Open();
        Assert.Equal(OrderStatus.Delivered, final.Orders.Single(x => x.Id == order).Status);
        Assert.Equal(12.50m, final.OrderItems.Single(x => x.OrderId == order).LineTotal);
        Assert.Equal(0, final.Products.Single(x => x.Id == scenario.ProductId).Inventory);
    }

    private static async Task<T[]> Together<T>(int count, Func<Task<T>> action)
    {
        using var barrier = new Barrier(count);
        return await Task.WhenAll(Enumerable.Range(0, count).Select(_ => Task.Run(async () =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Concurrent start timed out.");
            return await action();
        })));
    }
    private sealed class CheckoutBarrier(Barrier barrier) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Checkout overlap timed out.");
            return result;
        }
    }
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public bool Triggered { get; private set; }
        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        { Triggered = true; throw new InvalidOperationException("Injected failure after SQL writes, before order commit."); }
    }
    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Store.SqlIntegration";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
