using System.Collections;
using System.Data;
using System.Linq.Expressions;
using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Domain.Entities.Carts;
using FinalProject_Store.Domain.Entities.Common;
using FinalProject_Store.Domain.Entities.Orders;
using FinalProject_Store.Domain.Entities.Payments;
using FinalProject_Store.Domain.Entities.Products;
using FinalProject_Store.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

// Service-policy test double only: deliberately makes no claim to simulate SQL locks,
// query filters, isolation, rowversion generation, or transactional rollback.
internal sealed class TestContext : IDataBaseContext
{
    public DbSet<User> Users { get; set; } = new TestSet<User>();
    public DbSet<Role> Roles { get; set; } = new TestSet<Role>();
    public DbSet<UserInRole> UserInRoles { get; set; } = new TestSet<UserInRole>();
    public DbSet<Category> Categories { get; set; } = new TestSet<Category>();
    public DbSet<Product> Products { get; set; } = new TestSet<Product>();
    public DbSet<Cart> Carts { get; set; } = new TestSet<Cart>();
    public DbSet<CartItem> CartItems { get; set; } = new TestSet<CartItem>();
    public DbSet<Payment> Payments { get; set; } = new TestSet<Payment>();
    public DbSet<Order> Orders { get; set; } = new TestSet<Order>();
    public DbSet<OrderItem> OrderItems { get; set; } = new TestSet<OrderItem>();
    public int LockCalls { get; private set; }
    public IDbContextTransaction BeginTransaction(IsolationLevel isolationLevel) => new TestTransaction();
    public void LockOrder(long orderId) => LockCalls++;
    public void ClearTracking() { }
    public int SaveChanges()
    {
        foreach (var order in Orders)
        {
            order.User = Users.Single(x => x.Id == order.UserId);
            foreach (var item in order.Items) { item.OrderId = order.Id; item.Order = order; }
        }
        foreach (var payment in Payments)
        {
            payment.Order = Orders.Single(x => x.Id == payment.OrderId);
            if (!payment.Order.Payments.Contains(payment)) payment.Order.Payments.Add(payment);
        }
        foreach (var cart in Carts)
            foreach (var item in cart.Items.Where(x => !CartItems.Contains(x)).ToList()) cart.Items.Remove(item);
        return 1;
    }
    public int SaveChanges(bool acceptAllChangesOnSuccess) => SaveChanges();
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(SaveChanges());
    public Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) => SaveChangesAsync(cancellationToken);
}

internal sealed class TestSet<T> : DbSet<T>, IQueryable<T> where T : class
{
    private readonly List<T> items = new();
    private IQueryable<T> Query => items.AsQueryable();
    public override IEntityType EntityType => throw new NotSupportedException();
    Type IQueryable.ElementType => Query.ElementType;
    Expression IQueryable.Expression => Query.Expression;
    IQueryProvider IQueryable.Provider => Query.Provider;
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();
    public override EntityEntry<T> Add(T entity)
    {
        if (entity is BaseEntity row && row.Id == 0) row.Id = items.Count + 1;
        items.Add(entity);
        return null!;
    }
    public override EntityEntry<T> Remove(T entity) { items.Remove(entity); return null!; }
    public override void RemoveRange(IEnumerable<T> entities) { foreach (var entity in entities.ToList()) Remove(entity); }
}

internal sealed class TestTransaction : IDbContextTransaction
{
    public Guid TransactionId { get; } = Guid.NewGuid();
    public void Commit() { }
    public void Rollback() { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
