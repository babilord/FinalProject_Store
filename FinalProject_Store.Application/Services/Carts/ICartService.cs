using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using FinalProject_Store.Domain.Entities.Carts;
using FinalProject_Store.Domain.Entities.Products;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Carts
{
    public interface ICartService
    {
        ResultDto<CartDto> Get(long userId);
        ResultDto Add(long userId, long productId, int quantity);
        ResultDto UpdateQuantity(long userId, long cartItemId, int quantity);
        ResultDto Remove(long userId, long cartItemId);
    }

    public class CartService : ICartService
    {
        private readonly IDataBaseContext _context;
        public CartService(IDataBaseContext context) { _context = context; }

        public ResultDto<CartDto> Get(long userId)
        {
            var cart = _context.Carts.IgnoreQueryFilters()
                .Include(x => x.Items.Where(item => !item.IsRemoved)).ThenInclude(x => x.Product)
                .SingleOrDefault(x => x.UserId == userId && !x.IsRemoved);
            if (cart == null) return Success(new CartDto(), string.Empty);

            var adjusted = false;
            foreach (var item in cart.Items.ToList())
            {
                if (!CanBePurchased(item.Product))
                {
                    _context.CartItems.Remove(item);
                    adjusted = true;
                }
                else if (item.Quantity > item.Product.Inventory)
                {
                    item.Quantity = item.Product.Inventory;
                    item.UpdateDate = DateTime.Now;
                    adjusted = true;
                }
            }
            if (adjusted) _context.SaveChanges();

            var data = new CartDto
            {
                Items = cart.Items.Where(x => CanBePurchased(x.Product))
                    .OrderByDescending(x => x.Id)
                    .Select(x => new CartItemDto
                    {
                        Id = x.Id, ProductId = x.ProductId, ProductName = x.Product.Name,
                        Brand = x.Product.Brand, HasImage = !string.IsNullOrWhiteSpace(x.Product.ImageSrc),
                        UnitPrice = x.Product.Price, Quantity = x.Quantity,
                        AvailableInventory = x.Product.Inventory
                    }).ToList()
            };
            return Success(data, adjusted ? "سبد خرید با موجودی فعلی کالاها به‌روز شد." : string.Empty);
        }

        public ResultDto Add(long userId, long productId, int quantity)
        {
            if (userId <= 0 || productId <= 0) return Failure("درخواست افزودن کالا معتبر نیست.");
            if (quantity < 1) return Failure("تعداد کالا باید حداقل یک عدد باشد.");
            var product = _context.Products.SingleOrDefault(x => x.Id == productId);
            if (product == null || !CanBePurchased(product))
                return Failure("این کالا در حال حاضر قابل افزودن به سبد خرید نیست.");

            var cart = _context.Carts.Include(x => x.Items).SingleOrDefault(x => x.UserId == userId);
            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _context.Carts.Add(cart);
            }
            var item = cart.Items.SingleOrDefault(x => x.ProductId == productId);
            var requestedQuantity = quantity + (item?.Quantity ?? 0);
            if (requestedQuantity > product.Inventory)
                return Failure($"حداکثر تعداد قابل سفارش از این کالا {product.Inventory} عدد است.");
            if (item == null) cart.Items.Add(new CartItem { ProductId = productId, Quantity = quantity });
            else { item.Quantity = requestedQuantity; item.UpdateDate = DateTime.Now; }
            _context.SaveChanges();
            return Success("کالا با موفقیت به سبد خرید افزوده شد.");
        }

        public ResultDto UpdateQuantity(long userId, long cartItemId, int quantity)
        {
            if (quantity < 1) return Failure("تعداد کالا باید حداقل یک عدد باشد.");
            var item = _context.CartItems.Include(x => x.Product).Include(x => x.Cart)
                .SingleOrDefault(x => x.Id == cartItemId && x.Cart.UserId == userId);
            if (item == null) return Failure("آیتم موردنظر در سبد خرید شما یافت نشد.");
            if (!CanBePurchased(item.Product)) return Failure("این کالا دیگر قابل عرضه نیست؛ آن را از سبد خرید حذف کنید.");
            if (quantity > item.Product.Inventory)
                return Failure($"حداکثر تعداد قابل سفارش از این کالا {item.Product.Inventory} عدد است.");
            item.Quantity = quantity;
            item.UpdateDate = DateTime.Now;
            _context.SaveChanges();
            return Success("تعداد کالا به‌روز شد.");
        }

        public ResultDto Remove(long userId, long cartItemId)
        {
            var item = _context.CartItems.Include(x => x.Cart)
                .SingleOrDefault(x => x.Id == cartItemId && x.Cart.UserId == userId);
            if (item == null) return Failure("آیتم موردنظر در سبد خرید شما یافت نشد.");
            _context.CartItems.Remove(item);
            _context.SaveChanges();
            return Success("کالا از سبد خرید حذف شد.");
        }

        private static bool CanBePurchased(Product product) => !product.IsRemoved && product.IsActive && product.Inventory > 0;
        private static ResultDto Failure(string message) => new() { IsSuccess = false, Message = message };
        private static ResultDto Success(string message) => new() { IsSuccess = true, Message = message };
        private static ResultDto<CartDto> Success(CartDto data, string message) => new() { IsSuccess = true, Message = message, Data = data };
    }

    public class CartDto
    {
        public List<CartItemDto> Items { get; set; } = new();
        public decimal Total => Items.Sum(x => x.LineTotal);
    }

    public class CartItemDto
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public bool HasImage { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public int AvailableInventory { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }
}
