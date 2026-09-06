using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Roles;
using FinalProject_Store.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using FinalProject_Store.Domain.Entities.Products;
using FinalProject_Store.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
namespace FinalProject_Store.Persistence.Contexts
{
    public class DataBaseContext:DbContext, IDataBaseContext
    {
        public DataBaseContext(DbContextOptions<DataBaseContext> options) : base(options)
        {
        }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserInRole> UserInRoles { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<FinalProject_Store.Domain.Entities.Carts.Cart> Carts { get; set; }
        public DbSet<FinalProject_Store.Domain.Entities.Carts.CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        public IDbContextTransaction BeginTransaction(IsolationLevel isolationLevel) => Database.BeginTransaction(isolationLevel);

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return base.SaveChangesAsync(cancellationToken);
        }
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>().HasData(
                new Role
                {
                    Id = 1,
                    Name = nameof(UserRoles.Admin)
                });

            modelBuilder.Entity<Role>().HasData(
                new Role
                {
                    Id = 2,
                    Name = nameof(UserRoles.Operator)
                });

            modelBuilder.Entity<Role>().HasData(
                new Role
                {
                    Id = 3,
                    Name = nameof(UserRoles.Customer)
                });

            modelBuilder.Entity<User>()
                .HasIndex(user => user.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasQueryFilter(user => !user.IsRemoved);

            modelBuilder.Entity<Category>()
                .Property(category => category.Name)
                .IsRequired()
                .HasMaxLength(200);

            modelBuilder.Entity<Product>()
                .Property(product => product.Name)
                .IsRequired()
                .HasMaxLength(300);

            modelBuilder.Entity<Product>()
                .Property(product => product.Brand)
                .HasMaxLength(200);

            modelBuilder.Entity<Product>()
                .Property(product => product.Description)
                .HasMaxLength(4000);

            modelBuilder.Entity<Product>()
                .Property(product => product.ImageSrc)
                .HasMaxLength(500);

            modelBuilder.Entity<Product>()
                .Property(product => product.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .HasOne(product => product.Category)
                .WithMany(category => category.Products)
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Category>()
                .HasQueryFilter(category => !category.IsRemoved);

            modelBuilder.Entity<Product>()
                .HasQueryFilter(product => !product.IsRemoved);

            modelBuilder.Entity<FinalProject_Store.Domain.Entities.Carts.Cart>()
                .HasOne(cart => cart.User)
                .WithOne(user => user.Cart)
                .HasForeignKey<FinalProject_Store.Domain.Entities.Carts.Cart>(cart => cart.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FinalProject_Store.Domain.Entities.Carts.Cart>()
                .HasIndex(cart => cart.UserId).IsUnique();

            modelBuilder.Entity<FinalProject_Store.Domain.Entities.Carts.CartItem>()
                .HasOne(item => item.Cart)
                .WithMany(cart => cart.Items)
                .HasForeignKey(item => item.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FinalProject_Store.Domain.Entities.Carts.CartItem>()
                .HasOne(item => item.Product)
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FinalProject_Store.Domain.Entities.Carts.CartItem>()
                .HasIndex(item => new { item.CartId, item.ProductId }).IsUnique();

            modelBuilder.Entity<FinalProject_Store.Domain.Entities.Carts.Cart>()
                .HasQueryFilter(cart => !cart.IsRemoved);
            modelBuilder.Entity<FinalProject_Store.Domain.Entities.Carts.CartItem>()
                .HasQueryFilter(item => !item.IsRemoved);

            modelBuilder.Entity<Order>().Property(x => x.Total).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(x => x.FullName).IsRequired().HasMaxLength(200);
            modelBuilder.Entity<Order>().Property(x => x.MobileNumber).IsRequired().HasMaxLength(20);
            modelBuilder.Entity<Order>().Property(x => x.Province).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<Order>().Property(x => x.City).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<Order>().Property(x => x.PostalAddress).IsRequired().HasMaxLength(1000);
            modelBuilder.Entity<Order>().Property(x => x.PostalCode).IsRequired().HasMaxLength(20);
            modelBuilder.Entity<Order>().Property(x => x.Notes).HasMaxLength(1000);
            modelBuilder.Entity<Order>().HasOne(x => x.User).WithMany(x => x.Orders)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Order>().HasIndex(x => new { x.UserId, x.InsertTime });
            modelBuilder.Entity<Order>().HasQueryFilter(x => !x.IsRemoved);

            modelBuilder.Entity<OrderItem>().Property(x => x.ProductName).IsRequired().HasMaxLength(300);
            modelBuilder.Entity<OrderItem>().Property(x => x.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<OrderItem>().Property(x => x.LineTotal).HasPrecision(18, 2);
            modelBuilder.Entity<OrderItem>().HasOne(x => x.Order).WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<OrderItem>().HasOne(x => x.Product).WithMany()
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<OrderItem>().HasIndex(x => x.OrderId);
            modelBuilder.Entity<OrderItem>().HasQueryFilter(x => !x.IsRemoved);
        }
    }
}
