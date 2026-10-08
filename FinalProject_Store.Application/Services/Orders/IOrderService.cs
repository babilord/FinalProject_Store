using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.Extensions.Logging;
using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Common.Dto;
using FinalProject_Store.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Orders;

public interface IOrderService
{
    ResultDto<CheckoutDto> GetCheckout(long userId);
    ResultDto<long> Create(long userId, CreateOrderDto request);
    ResultDto<OrderDetailsDto> GetDetails(long userId, long orderId);
    ResultDto<PageResult<OrderListItemDto>> GetMyOrders(long userId, int page = 1);
}

public class OrderService : IOrderService
{
    private readonly IDataBaseContext _context;
    private readonly ILogger<OrderService> _logger;
    public OrderService(IDataBaseContext context, ILogger<OrderService> logger) { _context = context; _logger = logger; }

    public ResultDto<CheckoutDto> GetCheckout(long userId)
    {
        var cart = LoadCart(userId, false);
        if (cart == null || cart.Items.Count == 0)
            return Fail<CheckoutDto>("سبد خرید شما خالی است.");

        var error = ValidateItems(cart.Items);
        if (error != null) return Fail<CheckoutDto>(error);

        return Ok(new CheckoutDto { Items = MapSummary(cart.Items) });
    }

    public ResultDto<long> Create(long userId, CreateOrderDto request)
    {
        if (userId <= 0 || !_context.Users.Any(x => x.Id == userId && x.isActive)) return Fail<long>("کاربر معتبر نیست.");
        if (request == null) return Fail<long>("اطلاعات سفارش معتبر نیست.");
        var validation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validation, true))
            return Fail<long>(validation.First().ErrorMessage!);

        try
        {
            using var transaction = _context.BeginTransaction(IsolationLevel.Serializable);
            var cart = LoadCart(userId, true);
            if (cart == null || cart.Items.Count == 0)
                return Rollback(transaction, "سبد خرید شما خالی است.");

            var error = ValidateItems(cart.Items);
            if (error != null) return Rollback(transaction, error);

            var order = new Order
            {
                UserId = userId,
                Status = OrderStatus.PendingPayment,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
                FullName = request.FullName.Trim(),
                MobileNumber = request.MobileNumber.Trim(),
                Province = request.Province.Trim(),
                City = request.City.Trim(),
                PostalAddress = request.PostalAddress.Trim(),
                PostalCode = request.PostalCode.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            };

            foreach (var cartItem in cart.Items.OrderBy(x => x.ProductId))
            {
                var lineTotal = cartItem.Product.Price * cartItem.Quantity;
                order.Items.Add(new OrderItem
                {
                    ProductId = cartItem.ProductId,
                    ProductName = cartItem.Product.Name,
                    UnitPrice = cartItem.Product.Price,
                    Quantity = cartItem.Quantity,
                    LineTotal = lineTotal
                });
                order.Total += lineTotal;
                cartItem.Product.Inventory -= cartItem.Quantity;
                cartItem.Product.UpdateDate = DateTime.Now;
            }

            _context.Orders.Add(order);
            _context.CartItems.RemoveRange(cart.Items);
            _context.SaveChanges();
            transaction.Commit();
            return Ok(order.Id, "سفارش شما با موفقیت ثبت شد.");
        }
        catch (Exception ex)
        {
            _context.ClearTracking();
            _logger.LogError(ex, "Order creation/inventory conflict for user {UserId}", userId);
            return Fail<long>("ثبت سفارش به دلیل تغییر هم‌زمان موجودی انجام نشد. سبد خرید شما حفظ شده است؛ دوباره تلاش کنید.");
        }
    }

    public ResultDto<OrderDetailsDto> GetDetails(long userId, long orderId)
    {
        var order = _context.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.Payments)
            .SingleOrDefault(x => x.Id == orderId && x.UserId == userId);
        if (order == null) return Fail<OrderDetailsDto>("سفارش موردنظر یافت نشد.");

        return Ok(new OrderDetailsDto
        {
            Id = order.Id, InsertTime = order.InsertTime, Status = order.Status, Total = order.Total,
            FullName = order.FullName, MobileNumber = order.MobileNumber, Province = order.Province,
            City = order.City, PostalAddress = order.PostalAddress, PostalCode = order.PostalCode,
            Notes = order.Notes, ExpiresAtUtc = order.ExpiresAtUtc, ReservationExpired = order.ReservationExpired,
            LastPaymentStatus = order.Payments.OrderByDescending(x => x.Id).Select(x => (FinalProject_Store.Domain.Entities.Payments.PaymentStatus?)x.Status).FirstOrDefault(),
            PaymentReference = order.Payments.FirstOrDefault(x => x.Status == FinalProject_Store.Domain.Entities.Payments.PaymentStatus.Succeeded)?.ReferenceId,
            Items = order.Items.OrderBy(x => x.Id).Select(x => new OrderItemDto
            {
                ProductId = x.ProductId, ProductName = x.ProductName, UnitPrice = x.UnitPrice,
                Quantity = x.Quantity, LineTotal = x.LineTotal
            }).ToList()
        });
    }

    public ResultDto<PageResult<OrderListItemDto>> GetMyOrders(long userId, int page = 1)
    {
        var query = _context.Orders.AsNoTracking().Where(x => x.UserId == userId);
        var count = query.Count();
        var pages = Math.Max(1, (int)Math.Ceiling(count / 20d));
        page = Math.Clamp(page, 1, pages);
        return Ok(new PageResult<OrderListItemDto> { Page = page, Pages = pages, Count = count,
            Items = query.OrderByDescending(x => x.Id).Skip((page - 1) * 20).Take(20)
                .Select(x => new OrderListItemDto { Id = x.Id, InsertTime = x.InsertTime, Total = x.Total, Status = x.Status }).ToList() });
    }

    private Domain.Entities.Carts.Cart? LoadCart(long userId, bool tracking) =>
        (tracking ? _context.Carts.IgnoreQueryFilters() : _context.Carts.IgnoreQueryFilters().AsNoTracking())
        .Include(x => x.Items.Where(i => !i.IsRemoved)).ThenInclude(x => x.Product)
        .SingleOrDefault(x => x.UserId == userId && !x.IsRemoved);

    private static string? ValidateItems(IEnumerable<Domain.Entities.Carts.CartItem> items)
    {
        foreach (var item in items)
        {
            if (item.Product == null || item.Product.IsRemoved)
                return "یکی از کالاهای سبد خرید دیگر موجود نیست. لطفاً سبد خرید را بررسی کنید.";
            if (item.Product.Price <= 0) return "قیمت کالا معتبر نیست.";
            if (!item.Product.IsActive)
                return $"کالای «{item.Product.Name}» غیرفعال شده است.";
            if (item.Product.Inventory <= 0)
                return $"کالای «{item.Product.Name}» ناموجود است.";
            if (item.Quantity <= 0 || item.Quantity > item.Product.Inventory)
                return $"موجودی کالای «{item.Product.Name}» برای تعداد درخواستی کافی نیست.";
        }
        return null;
    }

    private static List<CheckoutItemDto> MapSummary(IEnumerable<Domain.Entities.Carts.CartItem> items) => items.Select(x => new CheckoutItemDto
    {
        ProductId = x.ProductId, ProductName = x.Product.Name,
        HasImage = !string.IsNullOrWhiteSpace(x.Product.ImageSrc), UnitPrice = x.Product.Price,
        Quantity = x.Quantity
    }).ToList();

    private static ResultDto<T> Ok<T>(T data, string message = "") => new() { IsSuccess = true, Data = data, Message = message };
    private static ResultDto<T> Fail<T>(string message) => new() { IsSuccess = false, Data = default!, Message = message };
    private static ResultDto<long> Rollback(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, string message)
    { transaction.Rollback(); return Fail<long>(message); }
}

public class CreateOrderDto : IValidatableObject
{
    [Required(ErrorMessage = "نام و نام خانوادگی الزامی است.")]
    [StringLength(200, ErrorMessage = "نام و نام خانوادگی نباید بیشتر از ۲۰۰ کاراکتر باشد.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره موبایل الزامی است.")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل باید با ۰۹ شروع شود و ۱۱ رقم باشد.")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "استان الزامی است.")]
    [StringLength(100, ErrorMessage = "نام استان نباید بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string Province { get; set; } = string.Empty;

    [Required(ErrorMessage = "شهر الزامی است.")]
    [StringLength(100, ErrorMessage = "نام شهر نباید بیشتر از ۱۰۰ کاراکتر باشد.")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "نشانی پستی الزامی است.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "نشانی پستی باید بین ۱۰ تا ۱۰۰۰ کاراکتر باشد.")]
    public string PostalAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "کد پستی الزامی است.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "کد پستی باید ۱۰ رقم باشد.")]
    public string PostalCode { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "توضیحات نباید بیشتر از ۱۰۰۰ کاراکتر باشد.")]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FullName)) yield return new ValidationResult("نام و نام خانوادگی الزامی است.", new[] { nameof(FullName) });
        if (string.IsNullOrWhiteSpace(Province)) yield return new ValidationResult("استان الزامی است.", new[] { nameof(Province) });
        if (string.IsNullOrWhiteSpace(City)) yield return new ValidationResult("شهر الزامی است.", new[] { nameof(City) });
        if (string.IsNullOrWhiteSpace(PostalAddress)) yield return new ValidationResult("نشانی پستی الزامی است.", new[] { nameof(PostalAddress) });
    }
}

public class CheckoutDto { public List<CheckoutItemDto> Items { get; set; } = new(); public decimal Total => Items.Sum(x => x.LineTotal); }
public class CheckoutItemDto { public long ProductId { get; set; } public string ProductName { get; set; } = ""; public bool HasImage { get; set; } public decimal UnitPrice { get; set; } public int Quantity { get; set; } public decimal LineTotal => UnitPrice * Quantity; }
public class OrderItemDto : CheckoutItemDto { public new decimal LineTotal { get; set; } }
public class OrderDetailsDto : CreateOrderDto { public DateTime ExpiresAtUtc { get; set; } public bool ReservationExpired { get; set; } public bool CanPay => Status == OrderStatus.PendingPayment && ExpiresAtUtc > DateTime.UtcNow; public FinalProject_Store.Domain.Entities.Payments.PaymentStatus? LastPaymentStatus { get; set; } public string? PaymentReference { get; set; } public long Id { get; set; } public DateTime InsertTime { get; set; } public OrderStatus Status { get; set; } public decimal Total { get; set; } public List<OrderItemDto> Items { get; set; } = new(); }
public class OrderListItemDto { public long Id { get; set; } public DateTime InsertTime { get; set; } public decimal Total { get; set; } public OrderStatus Status { get; set; } }
