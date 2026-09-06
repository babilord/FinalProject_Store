using FinalProject_Store.Domain.Entities.Products;
using FinalProject_Store.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using FinalProject_Store.Domain.Entities.Orders;

namespace FinalProject_Store.Application.Interfaces.Contexts
{
    public interface IDataBaseContext
    {
        DbSet<User> Users { get; set; }

        DbSet<Role> Roles { get; set; }

        DbSet<UserInRole> UserInRoles { get; set; }

        DbSet<Category> Categories { get; set; }

        DbSet<Product> Products { get; set; }

        DbSet<FinalProject_Store.Domain.Entities.Carts.Cart> Carts { get; set; }

        DbSet<FinalProject_Store.Domain.Entities.Carts.CartItem> CartItems { get; set; }

        DbSet<Order> Orders { get; set; }

        DbSet<OrderItem> OrderItems { get; set; }

        IDbContextTransaction BeginTransaction(IsolationLevel isolationLevel);

        int SaveChanges();

        int SaveChanges(bool acceptAllChangesOnSuccess);

        Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default);

        Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default);
    }
}
