using FinalProject_Store.Domain.Entities.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace FinalProject_Store.Domain.Entities.Users
{
    public class User:BaseEntity
    {
        public string FullName{ get; set; }
        public string Email{ get; set; }
        public string Password{ get; set; }
        public bool isActive {  get; set; }
        public ICollection<UserInRole> UserInRoles { get; set; }
        public FinalProject_Store.Domain.Entities.Carts.Cart? Cart { get; set; }
        public ICollection<FinalProject_Store.Domain.Entities.Orders.Order> Orders { get; set; } = new List<FinalProject_Store.Domain.Entities.Orders.Order>();
    }
}
