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
        }
    }
}
