using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Domain.Entities.Orders;
using FinalProject_Store.Domain.Entities.Payments;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_Store.Application.Services.Orders;

public interface IAdminOrderService
{
    PageResult<AdminOrderRow> Orders(string? search, OrderStatus? status, int page);
    AdminOrderDetails? Details(long id);
    PageResult<AdminPaymentRow> Payments(string? search, PaymentStatus? status, long? orderId, int page);
}

public class AdminOrderService(IDataBaseContext context) : IAdminOrderService
{
    public PageResult<AdminOrderRow> Orders(string? search, OrderStatus? status, int page)
    {
        var query = context.Orders.IgnoreQueryFilters().AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            long.TryParse(search, out var id);
            query = query.Where(x => x.Id == id || x.FullName.Contains(search) || x.User.FullName.Contains(search) || x.User.Email.Contains(search) || x.MobileNumber.Contains(search));
        }
        return Page(query.OrderByDescending(x => x.Id).Select(x => new AdminOrderRow
        {
            Id = x.Id, Customer = x.User.FullName, Email = x.User.Email, Total = x.Total,
            Status = x.Status, Created = x.InsertTime, IsRemoved = x.IsRemoved,
            PaymentStatus = x.Payments.OrderByDescending(p => p.Id).Select(p => (PaymentStatus?)p.Status).FirstOrDefault()
        }), page);
    }

    public AdminOrderDetails? Details(long id)
    {
        var order = context.Orders.IgnoreQueryFilters().AsNoTracking().Include(x => x.Items)
            .Include(x => x.User).SingleOrDefault(x => x.Id == id);
        return order == null ? null : new AdminOrderDetails
        {
            Order = order, Customer = order.User.FullName, Email = order.User.Email,
            Reference = context.Payments.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.OrderId == id && x.Status == PaymentStatus.Succeeded).Select(x => x.ReferenceId).FirstOrDefault()
        };
    }

    public PageResult<AdminPaymentRow> Payments(string? search, PaymentStatus? status, long? orderId, int page)
    {
        var query = context.Payments.IgnoreQueryFilters().AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (orderId.HasValue) query = query.Where(x => x.OrderId == orderId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            long.TryParse(search, out var id);
            query = query.Where(x => x.OrderId == id || (x.ReferenceId != null && x.ReferenceId.Contains(search)) || x.Gateway.Contains(search));
        }
        return Page(query.OrderByDescending(x => x.Id).Select(x => new AdminPaymentRow
        {
            Id = x.Id, OrderId = x.OrderId, Amount = x.Amount, Gateway = x.Gateway,
            Status = x.Status, Reference = x.ReferenceId, Created = x.InsertTime, Updated = x.UpdateDate,
            ExpiresAtUtc = x.ExpiresAtUtc
        }), page);
    }

    private static PageResult<T> Page<T>(IQueryable<T> query, int page)
    {
        var count = query.Count();
        var pages = Math.Max(1, (int)Math.Ceiling(count / 20d));
        page = Math.Clamp(page, 1, pages);
        return new() { Items = query.Skip((page - 1) * 20).Take(20).ToList(), Page = page, Pages = pages, Count = count };
    }
}

public class PageResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int Pages { get; set; }
    public int Count { get; set; }
}
public class AdminOrderRow
{
    public long Id { get; set; }
    public string Customer { get; set; } = "";
    public string Email { get; set; } = "";
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public DateTime Created { get; set; }
    public bool IsRemoved { get; set; }
}
public class AdminOrderDetails
{
    public Order Order { get; set; } = null!;
    public string Customer { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Reference { get; set; }
}
public class AdminPaymentRow
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Gateway { get; set; } = "";
    public PaymentStatus Status { get; set; }
    public string? Reference { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Updated { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
