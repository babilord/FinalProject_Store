using FinalProject_Store.Domain.Entities.Products;
using FinalProject_Store.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Interfaces.Contexts
{
    public interface IDataBaseContext
    {
        DbSet<User> Users { get; set; }

        DbSet<Role> Roles { get; set; }

        DbSet<UserInRole> UserInRoles { get; set; }

        DbSet<Category> Categories { get; set; }

        DbSet<Product> Products { get; set; }

        int SaveChanges();

        int SaveChanges(bool acceptAllChangesOnSuccess);

        Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default);

        Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default);
    }
}