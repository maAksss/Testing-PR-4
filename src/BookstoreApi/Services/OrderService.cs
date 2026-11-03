using BookstoreApi.Domain;

namespace BookstoreApi.Services;

public interface IOrderService
{
    Order Create(CreateOrderRequest request);
    Order GetById(Guid id);
    Order UpdateStatus(Guid id, OrderStatus newStatus);
    IReadOnlyList<Order> GetByCustomer(int customerId);
}

public class OrderService(IBookService books) : IOrderService
{
    private readonly Dictionary<Guid, Order> _orders = new();
    private readonly object _lock = new();

    public Order Create(CreateOrderRequest r)
    {
        if (r.CustomerId <= 0)
            throw new InvalidRequestException("Ідентифікатор клієнта має бути додатним.");
        if (r.Items is null || r.Items.Count == 0)
            throw new InvalidRequestException("Замовлення має містити хоча б одну книгу.");
        if (r.Items.Any(i => i.Quantity <= 0))
            throw new InvalidRequestException("Кількість у кожній позиції має бути більшою за 0.");

        // Атомарно перевіряє наявність і зменшує кількість на складі
        var items = books.Reserve(r.Items);

        var order = new Order(Guid.NewGuid(), r.CustomerId, items, OrderStatus.Processing, DateTime.UtcNow);
        lock (_lock) _orders[order.Id] = order;
        return order;
    }

    public Order GetById(Guid id)
    {
        lock (_lock) return Get(id);
    }

    public Order UpdateStatus(Guid id, OrderStatus newStatus)
    {
        lock (_lock)
        {
            var order = Get(id);
            var allowed = (order.Status, newStatus) is
                (OrderStatus.Processing, OrderStatus.Shipped) or
                (OrderStatus.Shipped, OrderStatus.Completed);

            if (!allowed)
                throw new InvalidStatusTransitionException(order.Status, newStatus);

            var updated = order with { Status = newStatus };
            _orders[id] = updated;
            return updated;
        }
    }

    public IReadOnlyList<Order> GetByCustomer(int customerId)
    {
        lock (_lock)
            return _orders.Values
                .Where(o => o.CustomerId == customerId)
                .OrderBy(o => o.CreatedAt)
                .ToList();
    }

    private Order Get(Guid id) =>
        _orders.TryGetValue(id, out var order)
            ? order
            : throw new NotFoundException($"Замовлення {id} не знайдено.");
}
