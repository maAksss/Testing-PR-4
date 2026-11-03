namespace BookstoreApi.Domain;

public enum OrderStatus
{
    Processing, // на обробці
    Shipped,    // відправлено
    Completed   // виконано
}

public record Book(
    int Id,
    string Isbn,
    string Title,
    string Author,
    string? Description,
    decimal Price,
    int Quantity);

public record OrderItem(int BookId, int Quantity, decimal UnitPrice);

public record Order(
    Guid Id,
    int CustomerId,
    IReadOnlyList<OrderItem> Items,
    OrderStatus Status,
    DateTime CreatedAt)
{
    public decimal Total => Items.Sum(i => i.Quantity * i.UnitPrice);
}

// ---- DTO запитів ----
public record CreateBookRequest(
    string Isbn,
    string Title,
    string Author,
    string? Description,
    decimal Price,
    int Quantity);

// Усі поля необов'язкові. ISBN можна передати лише з тим самим значенням.
public record UpdateBookRequest(
    string? Isbn,
    string? Title,
    string? Author,
    string? Description,
    decimal? Price,
    int? Quantity);

public record OrderItemRequest(int BookId, int Quantity);

public record CreateOrderRequest(int CustomerId, List<OrderItemRequest> Items);

public record UpdateOrderStatusRequest(OrderStatus Status);
