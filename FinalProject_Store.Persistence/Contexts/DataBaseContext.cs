using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Roles;
using FinalProject_Store.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
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
            modelBuilder.Entity<Role>().HasData(new Role { Id = 1 ,Name= nameof(UserRoles.Admin)});
            modelBuilder.Entity<Role>().HasData(new Role { Id = 2 ,Name= nameof(UserRoles.Operator)});
            modelBuilder.Entity<Role>().HasData(new Role { Id = 3 ,Name= nameof(UserRoles.Customer)});

            //اعمال ایندکس بر روی فیلد ایمیل
            // اعمال عدم تکراری بودن ایمیل
            modelBuilder.Entity<User>().HasIndex(u=>u.Email).IsUnique();
            // فقط یوزرهایی رو برگردون که حذف نشده باشند یعنی IsRemoved = false
            modelBuilder.Entity<User>().HasQueryFilter(u=>!u.IsRemoved);
        }
    }
}
