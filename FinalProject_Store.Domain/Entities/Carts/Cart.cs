using FinalProject_Store.Domain.Entities.Common;
using FinalProject_Store.Domain.Entities.Users;

namespace FinalProject_Store.Domain.Entities.Carts
{
    public class Cart : BaseEntity
    {
        public long UserId { get; set; }
        public User User { get; set; } = null!;
        public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }
}
