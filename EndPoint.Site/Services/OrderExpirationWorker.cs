using FinalProject_Store.Application.Interfaces.Contexts;
using FinalProject_Store.Application.Services.Orders;
using FinalProject_Store.Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;

namespace EndPoint.Site.Services;

public sealed class OrderExpirationWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<OrderExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Orders:ExpirationIntervalSeconds", 60), 5, 3600));
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                long[] ids;
                using (var scope = scopes.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IDataBaseContext>();
                    ids = await context.Orders.IgnoreQueryFilters().AsNoTracking()
                        .Where(x => x.Status == OrderStatus.PendingPayment && x.ExpiresAtUtc <= DateTime.UtcNow)
                        .OrderBy(x => x.ExpiresAtUtc).ThenBy(x => x.Id).Select(x => x.Id).Take(100)
                        .ToArrayAsync(stoppingToken);
                }
                foreach (var id in ids)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    // A fresh context prevents a rolled-back attempt contaminating the next order.
                    using var scope = scopes.CreateScope();
                    scope.ServiceProvider.GetRequiredService<IOrderLifecycleService>().Expire(id);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Order expiration sweep failed; will retry next interval"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
