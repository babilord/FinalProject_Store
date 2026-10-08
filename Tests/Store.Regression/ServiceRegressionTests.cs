using FinalProject_Store.Application.Common.Security;
using FinalProject_Store.Application.Services.Carts;
using FinalProject_Store.Application.Services.Orders;
using FinalProject_Store.Application.Services.Payments;
using FinalProject_Store.Application.Services.Users.Commands.UserLogin;
using FinalProject_Store.Domain.Entities.Carts;
using FinalProject_Store.Domain.Entities.Orders;
using FinalProject_Store.Domain.Entities.Payments;
using FinalProject_Store.Domain.Entities.Products;
using FinalProject_Store.Domain.Entities.Users;
using FinalProject_Store.Infrastructures.Payments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class ServiceFixture
{
    internal TestContext Db { get; } = new();
    public Product Product { get; } = new() { Id = 1, Name = "Original", Brand = "Brand", Price = 12.50m, Inventory = 5, IsActive = true };
    public User User { get; } = new() { Id = 1, FullName = "Customer", Email = "customer@example.test", Password = "legacy password", isActive = true };
    public Cart Cart { get; }
    public CartItem Item { get; }
    public OrderService Orders { get; }
    public CartService Carts { get; }
    public OrderLifecycleService Lifecycle { get; }
    public FakePaymentGateway Gateway { get; } = new(new EphemeralDataProtectionProvider(), new TestEnvironment());
    public PaymentService Payments { get; }
    public ServiceFixture()
    {
        Db.Users.Add(User); Db.Products.Add(Product);
        Cart = new Cart { Id = 1, UserId = 1, User = User };
        Item = new CartItem { Id = 1, Cart = Cart, CartId = 1, Product = Product, ProductId = 1, Quantity = 2 };
        Cart.Items.Add(Item); Db.Carts.Add(Cart); Db.CartItems.Add(Item);
        Orders = new(Db, NullLogger<OrderService>.Instance);
        Carts = new(Db); Lifecycle = new(Db, NullLogger<OrderLifecycleService>.Instance);
        Payments = new(Db, Gateway, NullLogger<PaymentService>.Instance);
    }
    public static CreateOrderDto Input() => new() { FullName = " Customer ", MobileNumber = "09123456789", Province = " Province ", City = " City ", PostalAddress = " A valid address ", PostalCode = "1234567890", Notes = " Note " };
    public Order CreateOrder()
    {
        Assert.True(Orders.Create(1, Input()).IsSuccess);
        return Db.Orders.Single();
    }
    public async Task<Payment> StartPayment()
    {
        Assert.True((await Payments.PayAsync(1, CreateOrder().Id)).IsSuccess);
        return Db.Payments.Single();
    }
    public string Receipt(Payment payment, PaymentStatus status) => Gateway.IssueReceipt(payment.Token, payment.OrderId, payment.Amount, payment.ExpiresAtUtc, status);
}

public class ServiceRegressionTests
{
    [Theory]
    [InlineData("", "legacy password")]
    [InlineData("customer@example.test", "")]
    [InlineData("unknown@example.test", "legacy password")]
    [InlineData("customer@example.test", "wrong")]
    public void InvalidLoginDoesNotChangePassword(string email, string password)
    {
        var f = new ServiceFixture();
        var login = new UserLoginService(f.Db, new PasswordHasher());
        Assert.False(login.Execute(new() { Email = email, Password = password }).IsSuccess);
        Assert.Equal("legacy password", f.User.Password);
    }

    [Fact]
    public void LegacyLoginNormalizesEmailUpgradesPasswordAndReturnsDistinctRoles()
    {
        var f = new ServiceFixture(); var hasher = new PasswordHasher();
        f.Db.Roles.Add(new Role { Id = 3, Name = "Customer" });
        f.Db.UserInRoles.Add(new UserInRole { UserId = 1, RoleId = 3 });
        f.Db.UserInRoles.Add(new UserInRole { UserId = 1, RoleId = 3 });
        var login = new UserLoginService(f.Db, hasher);
        var result = login.Execute(new() { Email = " CUSTOMER@EXAMPLE.TEST ", Password = "legacy password" });
        Assert.True(result.IsSuccess); Assert.Equal(new[] { "Customer" }, result.Data.Roles);
        Assert.True(hasher.VerifyPassword(f.User.Password, "legacy password"));
        Assert.True(login.Execute(new() { Email = f.User.Email, Password = "legacy password" }).IsSuccess);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void InactiveRemovedOrRolelessAccountsCannotLogin(bool active, bool removed, bool withRole)
    {
        var f = new ServiceFixture(); f.User.isActive = active; f.User.IsRemoved = removed;
        if (withRole) { f.Db.Roles.Add(new Role { Id = 3, Name = "Customer" }); f.Db.UserInRoles.Add(new UserInRole { UserId = 1, RoleId = 3 }); }
        Assert.False(new UserLoginService(f.Db, new PasswordHasher()).Execute(new() { Email = f.User.Email, Password = f.User.Password }).IsSuccess);
    }

    [Theory]
    [InlineData("PBKDF2$V1$0$AA==$AA==")]
    [InlineData("PBKDF2$V1$-1$AA==$AA==")]
    [InlineData("PBKDF2$V1$100000$AA==$")]
    [InlineData("PBKDF2$V1$bad$AA==$AA==")]
    [InlineData("PBKDF2$V1$100000$not-base64$AA==")]
    public void MalformedStoredHashesFailClosed(string hash) => Assert.False(new PasswordHasher().VerifyPassword(hash, "password"));

    [Fact]
    public void PasswordSaltsAreUniqueAndWrongPasswordFails()
    {
        var h = new PasswordHasher(); var first = h.HashPassword("password");
        Assert.NotEqual(first, h.HashPassword("password")); Assert.True(h.VerifyPassword(first, "password"));
        Assert.False(h.VerifyPassword(first, "wrong"));
    }

    [Fact]
    public void EmptyStoredDigestCannotAuthenticateWithAnyPassword()
    {
        var f = new ServiceFixture(); f.User.Password = "PBKDF2$V1$100000$AAAAAAAAAAAAAAAAAAAAAA==$";
        f.Db.Roles.Add(new Role { Id = 3, Name = "Customer" }); f.Db.UserInRoles.Add(new UserInRole { UserId = 1, RoleId = 3 });
        Assert.False(new UserLoginService(f.Db, new PasswordHasher()).Execute(new() { Email = f.User.Email, Password = "any password" }).IsSuccess);
    }

    [Fact]
    public void FirstCartAdditionCreatesOneCartAndMergesRepeatedProduct()
    {
        var f = new ServiceFixture(); f.Db.CartItems.Remove(f.Item); f.Db.Carts.Remove(f.Cart);
        Assert.True(f.Carts.Add(1, 1, 1).IsSuccess); Assert.True(f.Carts.Add(1, 1, 2).IsSuccess);
        Assert.Single(f.Db.Carts); Assert.Equal(3, Assert.Single(f.Db.CartItems).Quantity); Assert.Equal(5, f.Product.Inventory);
    }

    [Theory]
    [InlineData("inactive")] [InlineData("removed")] [InlineData("empty")]
    public void CartCannotAddUnavailableProduct(string reason)
    {
        var f = new ServiceFixture();
        if (reason == "inactive") f.Product.IsActive = false;
        if (reason == "removed") f.Product.IsRemoved = true;
        if (reason == "empty") f.Product.Inventory = 0;
        Assert.False(f.Carts.Add(1, 1, 1).IsSuccess); Assert.Equal(2, f.Item.Quantity);
    }

    [Fact]
    public void MissingOrderAndInvalidStatusAreRejected()
    {
        var f = new ServiceFixture(); var order = f.CreateOrder();
        Assert.False(f.Lifecycle.Cancel(1, 999).IsSuccess); Assert.False(f.Lifecycle.Expire(999));
        Assert.False(f.Lifecycle.Advance(999, OrderStatus.Processing, 99).IsSuccess);
        Assert.False(f.Lifecycle.Advance(order.Id, (OrderStatus)999, 99).IsSuccess); Assert.Equal(3, f.Product.Inventory);
    }

    [Theory]
    [InlineData(0, 1, 1)] [InlineData(1, 0, 1)] [InlineData(1, 999, 1)]
    [InlineData(1, 1, 0)] [InlineData(1, 1, -1)] [InlineData(1, 1, 6)] [InlineData(1, 1, int.MaxValue)]
    public void InvalidCartAdditionLeavesStockAndQuantityUnchanged(long user, long product, int quantity)
    {
        var f = new ServiceFixture(); Assert.False(f.Carts.Add(user, product, quantity).IsSuccess);
        Assert.Equal(2, f.Item.Quantity); Assert.Equal(5, f.Product.Inventory);
    }

    [Fact]
    public void CartAddUpdateReadAndRemoveUseCurrentPriceWithoutReservingStock()
    {
        var f = new ServiceFixture(); Assert.True(f.Carts.Add(1, 1, 1).IsSuccess); Assert.Equal(3, f.Item.Quantity);
        Assert.True(f.Carts.UpdateQuantity(1, 1, 4).IsSuccess);
        f.Product.Price = 20; Assert.Equal(80, f.Carts.Get(1).Data.Total); Assert.Equal(5, f.Product.Inventory);
        Assert.True(f.Carts.Remove(1, 1).IsSuccess); Assert.Empty(f.Carts.Get(1).Data.Items);
        Assert.False(f.Carts.Remove(1, 1).IsSuccess);
    }

    [Theory]
    [InlineData(2, 1, 1)] [InlineData(1, 999, 1)] [InlineData(1, 1, 0)] [InlineData(1, 1, -1)] [InlineData(1, 1, 6)]
    public void InvalidCartUpdateDoesNotMutate(long user, long item, int quantity)
    {
        var f = new ServiceFixture(); Assert.False(f.Carts.UpdateQuantity(user, item, quantity).IsSuccess);
        Assert.Equal(2, f.Item.Quantity);
        Assert.False(f.Carts.Remove(2, 1).IsSuccess); Assert.Single(f.Cart.Items);
    }

    [Theory]
    [InlineData("inactive")] [InlineData("removed")] [InlineData("empty")]
    public void CartDropsUnavailableProducts(string state)
    {
        var f = new ServiceFixture();
        if (state == "inactive") f.Product.IsActive = false;
        if (state == "removed") f.Product.IsRemoved = true;
        if (state == "empty") f.Product.Inventory = 0;
        Assert.Empty(f.Carts.Get(1).Data.Items); Assert.Empty(f.Db.CartItems);
    }

    [Fact]
    public void CartClampsQuantityWhenInventoryFalls()
    {
        var f = new ServiceFixture(); f.Product.Inventory = 1;
        Assert.Equal(1, Assert.Single(f.Carts.Get(1).Data.Items).Quantity); Assert.Equal(1, f.Item.Quantity);
    }

    [Fact]
    public void CheckoutCreatesSnapshotsAndThirtyMinuteReservationAndEmptiesCart()
    {
        var f = new ServiceFixture(); var before = DateTime.UtcNow;
        Assert.Equal(25, f.Orders.GetCheckout(1).Data.Total);
        var order = f.CreateOrder(); var after = DateTime.UtcNow;
        Assert.InRange(order.ExpiresAtUtc, before.AddMinutes(30), after.AddMinutes(30));
        Assert.Equal(OrderStatus.PendingPayment, order.Status); Assert.Equal(25, order.Total);
        Assert.Equal("Customer", order.FullName); Assert.Equal("Note", order.Notes);
        Assert.Equal(3, f.Product.Inventory); Assert.Empty(f.Db.CartItems); Assert.Empty(f.Cart.Items);
        f.Product.Price = 99; f.Product.Name = "Changed";
        var detail = f.Orders.GetDetails(1, order.Id).Data;
        Assert.Equal("Original", Assert.Single(detail.Items).ProductName); Assert.Equal(25, detail.Total);
        Assert.False(f.Orders.GetDetails(2, order.Id).IsSuccess);
        Assert.Empty(f.Orders.GetMyOrders(2).Data.Items);
        Assert.Equal(1, f.Orders.GetMyOrders(1, -10).Data.Page);
        Assert.False(f.Orders.Create(1, ServiceFixture.Input()).IsSuccess); Assert.Single(f.Db.Orders);
    }

    [Fact]
    public void NullCheckoutRequestReturnsFailure()
    {
        var f = new ServiceFixture(); Assert.False(f.Orders.Create(1, null!).IsSuccess);
        Assert.Equal(5, f.Product.Inventory); Assert.Empty(f.Db.Orders);
    }

    [Theory]
    [InlineData("name")] [InlineData("mobile")] [InlineData("postal")] [InlineData("address")]
    [InlineData("province")] [InlineData("city")] [InlineData("notes")]
    public void InvalidShippingDoesNotReserveStock(string field)
    {
        var f = new ServiceFixture(); var input = ServiceFixture.Input();
        switch (field) { case "name": input.FullName = " "; break; case "mobile": input.MobileNumber = "123"; break;
            case "postal": input.PostalCode = "ABC"; break; case "address": input.PostalAddress = "short"; break;
            case "province": input.Province = " "; break; case "city": input.City = " "; break; case "notes": input.Notes = new string('x', 1001); break; }
        Assert.False(f.Orders.Create(1, input).IsSuccess); Assert.Equal(5, f.Product.Inventory);
        Assert.Empty(f.Db.Orders); Assert.Single(f.Cart.Items);
    }

    [Theory]
    [InlineData("inactive")] [InlineData("removed")] [InlineData("stock")] [InlineData("quantity")]
    [InlineData("price")] [InlineData("user")] [InlineData("missing")]
    public void InvalidCheckoutDoesNotMutateCartOrStock(string reason)
    {
        var f = new ServiceFixture();
        switch (reason) { case "inactive": f.Product.IsActive = false; break; case "removed": f.Product.IsRemoved = true; break;
            case "stock": f.Product.Inventory = 1; break; case "quantity": f.Item.Quantity = 0; break;
            case "price": f.Product.Price = 0; break; case "user": f.User.isActive = false; break; case "missing": f.Item.Product = null!; break; }
        var stock = f.Product.Inventory;
        Assert.False(f.Orders.Create(1, ServiceFixture.Input()).IsSuccess);
        Assert.Equal(stock, f.Product.Inventory); Assert.Empty(f.Db.Orders); Assert.Single(f.Cart.Items);
    }

    [Theory]
    [InlineData(PaymentStatus.Succeeded, OrderStatus.Paid)]
    [InlineData(PaymentStatus.Failed, OrderStatus.PendingPayment)]
    [InlineData(PaymentStatus.Cancelled, OrderStatus.PendingPayment)]
    public async Task OutcomesAndConflictingDuplicateCallbacksAreIdempotent(PaymentStatus status, OrderStatus orderStatus)
    {
        var f = new ServiceFixture(); var payment = await f.StartPayment();
        var receipt = f.Receipt(payment, status);
        for (var i = 0; i < 3; i++) Assert.True((await f.Payments.CallbackAsync(payment.Token, receipt)).IsSuccess);
        var conflict = status == PaymentStatus.Succeeded ? PaymentStatus.Failed : PaymentStatus.Succeeded;
        Assert.True((await f.Payments.CallbackAsync(payment.Token, f.Receipt(payment, conflict))).IsSuccess);
        Assert.Equal(status, payment.Status); Assert.Equal(orderStatus, f.Db.Orders.Single().Status);
        Assert.Equal(3, f.Product.Inventory);
        Assert.Equal(status == PaymentStatus.Succeeded, payment.ReferenceId != null);
    }

    [Theory]
    [InlineData(PaymentStatus.Failed)] [InlineData(PaymentStatus.Cancelled)]
    public async Task FailedOrCancelledAttemptCanRetryWithNewToken(PaymentStatus status)
    {
        var f = new ServiceFixture(); var old = await f.StartPayment();
        await f.Payments.CallbackAsync(old.Token, f.Receipt(old, status));
        Assert.True((await f.Payments.PayAsync(1, old.OrderId)).IsSuccess);
        var retry = f.Db.Payments.Single(x => x.Status == PaymentStatus.Pending);
        Assert.NotEqual(old.Token, retry.Token);
        await f.Payments.CallbackAsync(retry.Token, f.Receipt(retry, PaymentStatus.Succeeded));
        await f.Payments.CallbackAsync(old.Token, f.Receipt(old, PaymentStatus.Succeeded));
        Assert.Equal(status, old.Status); Assert.Equal(OrderStatus.Paid, retry.Order.Status);
        Assert.Single(f.Db.Payments.Where(x => x.Status == PaymentStatus.Succeeded)); Assert.Equal(3, f.Product.Inventory);
    }

    [Theory]
    [InlineData("empty")] [InlineData("unknown")] [InlineData("long-token")] [InlineData("long-receipt")]
    [InlineData("wrong-order")] [InlineData("wrong-amount")] [InlineData("wrong-token")] [InlineData("expired-receipt")]
    public async Task InvalidCallbacksLeaveAttemptPending(string reason)
    {
        var f = new ServiceFixture(); var p = await f.StartPayment(); var token = p.Token; var receipt = f.Receipt(p, PaymentStatus.Succeeded);
        switch (reason) { case "empty": receipt = " "; break; case "unknown": token = "unknown"; break;
            case "long-token": token = new string('a', 201); break; case "long-receipt": receipt = new string('a', 4097); break;
            case "wrong-order": receipt = f.Gateway.IssueReceipt(token, 999, p.Amount, p.ExpiresAtUtc, PaymentStatus.Succeeded); break;
            case "wrong-amount": receipt = f.Gateway.IssueReceipt(token, p.OrderId, 999, p.ExpiresAtUtc, PaymentStatus.Succeeded); break;
            case "wrong-token": receipt = f.Gateway.IssueReceipt("other", p.OrderId, p.Amount, p.ExpiresAtUtc, PaymentStatus.Succeeded); break;
            case "expired-receipt": receipt = f.Gateway.IssueReceipt(token, p.OrderId, p.Amount, DateTime.UtcNow.AddSeconds(-1), PaymentStatus.Succeeded); break; }
        Assert.False((await f.Payments.CallbackAsync(token, receipt)).IsSuccess);
        Assert.Equal(PaymentStatus.Pending, p.Status); Assert.Equal(OrderStatus.PendingPayment, p.Order.Status); Assert.Equal(3, f.Product.Inventory);
    }

    [Fact]
    public async Task ExpiredPaymentAttemptIsReplacedWithinOrderDeadline()
    {
        var f = new ServiceFixture(); var p = await f.StartPayment(); p.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
        Assert.True((await f.Payments.PayAsync(1, p.OrderId)).IsSuccess);
        Assert.Equal(PaymentStatus.Cancelled, p.Status);
        var next = f.Db.Payments.Single(x => x.Status == PaymentStatus.Pending);
        Assert.True(next.ExpiresAtUtc <= next.Order.ExpiresAtUtc); Assert.Equal(3, f.Product.Inventory);
    }

    [Theory]
    [InlineData("total")] [InlineData("line")] [InlineData("quantity")] [InlineData("no-items")]
    public async Task InvalidOrderAmountsCannotStartPayment(string reason)
    {
        var f = new ServiceFixture(); var order = f.CreateOrder();
        switch (reason) { case "total": order.Total++; break; case "line": order.Items.Single().LineTotal++; break;
            case "quantity": order.Items.Single().Quantity = 0; break; case "no-items": order.Items.Clear(); break; }
        Assert.False((await f.Payments.PayAsync(1, order.Id)).IsSuccess); Assert.Empty(f.Db.Payments);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancellationAndExpirationRestoreExactlyOnceAndRejectLateSuccess(bool expired)
    {
        var f = new ServiceFixture(); var p = await f.StartPayment(); var receipt = f.Receipt(p, PaymentStatus.Succeeded);
        if (expired) p.Order.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
        Assert.True(expired ? f.Lifecycle.Expire(p.OrderId) : f.Lifecycle.Cancel(1, p.OrderId).IsSuccess);
        for (var i = 0; i < 3; i++) { f.Lifecycle.Cancel(1, p.OrderId); f.Lifecycle.Expire(p.OrderId); await f.Payments.CallbackAsync(p.Token, receipt); }
        Assert.Equal(5, f.Product.Inventory); Assert.Equal(OrderStatus.Cancelled, p.Order.Status);
        Assert.Equal(expired, p.Order.ReservationExpired); Assert.Equal(PaymentStatus.Cancelled, p.Status);
    }

    [Fact]
    public void RestorationGroupsHistoricalItemsAndIncludesRemovedProducts()
    {
        var f = new ServiceFixture(); var order = f.CreateOrder();
        order.Items.Clear(); order.Items.Add(new() { ProductId = 1, Quantity = 1 }); order.Items.Add(new() { ProductId = 1, Quantity = 1 });
        f.Product.IsRemoved = true; order.IsRemoved = true; order.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
        Assert.True(f.Lifecycle.Expire(order.Id)); Assert.Equal(5, f.Product.Inventory);
        Assert.False(f.Lifecycle.Expire(order.Id)); Assert.Equal(5, f.Product.Inventory);
    }

    public static IEnumerable<object[]> Transitions() => from a in Enum.GetValues<OrderStatus>() from b in Enum.GetValues<OrderStatus>() select new object[] { a, b };
    [Theory, MemberData(nameof(Transitions))]
    public void LifecycleEnforcesEveryStatusTransition(OrderStatus from, OrderStatus to)
    {
        var f = new ServiceFixture(); var order = f.CreateOrder(); order.Status = from;
        var expected = (from, to) is (OrderStatus.Paid, OrderStatus.Processing) or (OrderStatus.Processing, OrderStatus.Shipped) or (OrderStatus.Shipped, OrderStatus.Delivered);
        Assert.Equal(expected, f.Lifecycle.Advance(order.Id, to, 99).IsSuccess);
        Assert.Equal(expected ? to : from, order.Status); Assert.Equal(3, f.Product.Inventory);
    }

    [Fact]
    public async Task ConcurrentGatewayRequestsHaveUniqueTokensAndIndependentReceipts()
    {
        var f = new ServiceFixture();
        var requests = await Task.WhenAll(Enumerable.Range(1, 32).Select(i => Task.Run(() => f.Gateway.RequestAsync(i, i * 10))));
        Assert.Equal(32, requests.Select(x => x.Token).Distinct().Count());
        for (var i = 0; i < requests.Length; i++) {
            var r = requests[i]; var receipt = f.Gateway.IssueReceipt(r.Token, i + 1, (i + 1) * 10, r.ExpiresAtUtc, PaymentStatus.Succeeded);
            Assert.True((await f.Gateway.VerifyAsync(r.Token, receipt)).IsVerified);
            Assert.False((await f.Gateway.VerifyAsync(requests[(i + 1) % requests.Length].Token, receipt)).IsVerified);
        }
    }

    [Fact]
    public async Task ConcurrentPaymentRequestsUnderSerializedTransactionContractReuseOneAttempt()
    {
        var f = new ServiceFixture(); var order = f.CreateOrder(); f.Db.SerializeTransactions = true;
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => f.Payments.PayAsync(1, order.Id))));
        Assert.All(results, r => Assert.True(r.IsSuccess)); Assert.Single(f.Db.Payments);
        Assert.Single(results.Select(x => x.Data).Distinct()); Assert.Equal(3, f.Product.Inventory);
        Assert.Equal(16, f.Db.LockCalls);
    }

    [Fact]
    public async Task ConcurrentSuccessCallbacksUnderSerializedTransactionContractCompleteOnce()
    {
        var f = new ServiceFixture(); var payment = await f.StartPayment(); var receipt = f.Receipt(payment, PaymentStatus.Succeeded);
        f.Db.SerializeTransactions = true;
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => f.Payments.CallbackAsync(payment.Token, receipt))));
        Assert.All(results, r => Assert.True(r.IsSuccess)); Assert.Single(f.Db.Payments.Where(x => x.Status == PaymentStatus.Succeeded));
        Assert.Equal(OrderStatus.Paid, payment.Order.Status); Assert.Equal(3, f.Product.Inventory);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConcurrentCancellationAndCallbacksUnderSerializedTransactionContractHaveOneTerminalOutcome(bool expired)
    {
        var f = new ServiceFixture(); var p = await f.StartPayment(); var receipt = f.Receipt(p, PaymentStatus.Succeeded);
        if (expired) p.Order.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
        f.Db.SerializeTransactions = true;
        var jobs = Enumerable.Range(0, 16).Select(i => Task.Run(async () => {
            if (i % 3 == 0) await f.Payments.CallbackAsync(p.Token, receipt);
            else if (i % 3 == 1) f.Lifecycle.Cancel(1, p.OrderId);
            else f.Lifecycle.Expire(p.OrderId);
        }));
        await Task.WhenAll(jobs);
        Assert.Contains(p.Order.Status, new[] { OrderStatus.Paid, OrderStatus.Cancelled });
        Assert.Equal(p.Order.Status == OrderStatus.Paid ? 3 : 5, f.Product.Inventory);
        Assert.Equal(p.Order.Status == OrderStatus.Paid ? PaymentStatus.Succeeded : PaymentStatus.Cancelled, p.Status);
        if (expired) Assert.Equal(OrderStatus.Cancelled, p.Order.Status);
    }

    [Fact]
    public async Task FakeGatewayRejectsProductionAndInvalidOutcome()
    {
        var gateway = new FakePaymentGateway(new EphemeralDataProtectionProvider(), new TestEnvironment { EnvironmentName = "Production" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.RequestAsync(1, 10));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.VerifyAsync("token", "receipt"));
        Assert.Throws<InvalidOperationException>(() => gateway.IssueReceipt("token", 1, 10, DateTime.UtcNow.AddMinutes(1), PaymentStatus.Succeeded));
        var f = new ServiceFixture(); Assert.Throws<ArgumentOutOfRangeException>(() => f.Receipt(new Payment(), PaymentStatus.Pending));
    }
}
