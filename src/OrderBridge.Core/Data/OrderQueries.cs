using Microsoft.EntityFrameworkCore;

namespace OrderBridge.Core;

public record OrderPage(int Total, List<Order> Items);
public record OrderStatusCount(string Status, int Count);

/// <summary>Read operations for the order workspace. Writes belong to application services.</summary>
public class OrderQueries(BridgeDb db)
{
    public Task<List<Product>> Products() =>
        db.Products.AsNoTracking().OrderBy(product => product.Sku).ToListAsync();

    public Task<List<OrderStatusCount>> StatusCounts() =>
        db.Orders.AsNoTracking().GroupBy(order => order.Status)
            .Select(group => new OrderStatusCount(group.Key, group.Count())).ToListAsync();

    public async Task<OrderPage> Search(string? search, string? status, int page)
    {
        var query = db.Orders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(order => order.Customer.Contains(search) || order.Id.ToString().Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(order => order.Status == status);
        }

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(order => order.CreatedAt)
            .Skip((Math.Clamp(page, 1, 100000) - 1) * 10).Take(10).ToListAsync();
        return new OrderPage(total, items);
    }

    public Task<Order?> Find(Guid id) =>
        db.Orders.AsNoTracking().Include(order => order.Events).SingleOrDefaultAsync(order => order.Id == id);

    public Task<int> ReservationCount(Guid id) =>
        db.Reservations.CountAsync(reservation => reservation.OrderId == id && reservation.Reserved);
}
