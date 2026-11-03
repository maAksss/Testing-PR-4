using BookstoreApi.Domain;
using BookstoreApi.Services;

namespace BookstoreApi.Tests;

public class OrderServiceTests
{
    private readonly BookService _books = new();
    private readonly OrderService _orders;

    public OrderServiceTests()
    {
        _orders = new OrderService(_books);
    }

    private Book AddBook(string isbn = "111", int qty = 10, decimal price = 100m) =>
        _books.Add(new CreateBookRequest(isbn, "Title " + isbn, "Author", null, price, qty));

    private Order PlaceOrder(int customerId, int bookId, int qty = 1) =>
        _orders.Create(new CreateOrderRequest(customerId, [new OrderItemRequest(bookId, qty)]));

    private int Stock(Book b) => _books.GetById(b.Id).Quantity;

    // ---------- Оформлення замовлення ----------

    [Fact]
    public void Create_BookInStock_Succeeds()
    {
        var book = AddBook(qty: 10);

        var order = PlaceOrder(1, book.Id, 3);

        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Equal(300m, order.Total);
    }

    [Fact]
    public void Create_BookDoesNotExist_Throws()
    {
        Assert.Throws<NotFoundException>(() => PlaceOrder(1, bookId: 999));
    }

    [Fact]
    public void Create_InsufficientStock_ThrowsAndStockUnchanged()
    {
        var book = AddBook(qty: 2);

        Assert.Throws<InsufficientStockException>(() => PlaceOrder(1, book.Id, 3));

        Assert.Equal(2, Stock(book));
    }

    [Fact]
    public void Create_ExactlyAllCopies_IsAllowedAndStockBecomesZero()
    {
        var book = AddBook(qty: 4);

        PlaceOrder(1, book.Id, 4);

        Assert.Equal(0, Stock(book));
    }

    [Fact]
    public void Create_BookWithZeroStock_Throws()
    {
        var book = AddBook(qty: 0);

        Assert.Throws<InsufficientStockException>(() => PlaceOrder(1, book.Id));
    }

    // ---------- Зменшення кількості ----------

    [Fact]
    public void Create_DecreasesInventory()
    {
        var book = AddBook(qty: 10);

        PlaceOrder(1, book.Id, 4);

        Assert.Equal(6, Stock(book));
    }

    [Fact]
    public void Create_SameBookTwiceInOneOrder_QuantitiesAreSummed()
    {
        var book = AddBook(qty: 5);

        Assert.Throws<InsufficientStockException>(() =>
            _orders.Create(new CreateOrderRequest(1,
                [new OrderItemRequest(book.Id, 3), new OrderItemRequest(book.Id, 3)])));

        Assert.Equal(5, Stock(book));
    }

    [Fact]
    public void Create_MultipleItems_IfOneFailsNothingIsReserved()
    {
        var a = AddBook("A", qty: 10);
        var b = AddBook("B", qty: 1);

        Assert.Throws<InsufficientStockException>(() =>
            _orders.Create(new CreateOrderRequest(1,
                [new OrderItemRequest(a.Id, 5), new OrderItemRequest(b.Id, 2)])));

        Assert.Equal(10, Stock(a));
        Assert.Equal(1, Stock(b));
        Assert.Empty(_orders.GetByCustomer(1));
    }

    // ---------- Створення замовлення: ідентифікатор і деталі ----------

    [Fact]
    public void Create_AssignsUniqueIds()
    {
        var book = AddBook(qty: 10);

        var ids = Enumerable.Range(0, 5).Select(_ => PlaceOrder(1, book.Id).Id).ToList();

        Assert.Equal(5, ids.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, ids);
    }

    [Fact]
    public void Create_ContainsCustomerBooksAndQuantity()
    {
        var a = AddBook("A", qty: 10, price: 50m);
        var b = AddBook("B", qty: 10, price: 70m);

        var order = _orders.Create(new CreateOrderRequest(7,
            [new OrderItemRequest(a.Id, 2), new OrderItemRequest(b.Id, 1)]));

        Assert.Equal(7, order.CustomerId);
        Assert.Equal(2, order.Items.Count);
        Assert.Contains(order.Items, i => i.BookId == a.Id && i.Quantity == 2 && i.UnitPrice == 50m);
        Assert.Contains(order.Items, i => i.BookId == b.Id && i.Quantity == 1 && i.UnitPrice == 70m);
        Assert.Equal(170m, order.Total);
    }

    [Fact]
    public void Create_EmptyItems_Throws()
    {
        Assert.Throws<InvalidRequestException>(() =>
            _orders.Create(new CreateOrderRequest(1, [])));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Create_NonPositiveQuantity_Throws(int qty)
    {
        var book = AddBook();

        Assert.Throws<InvalidRequestException>(() => PlaceOrder(1, book.Id, qty));
    }

    [Fact]
    public void Create_InvalidCustomerId_Throws()
    {
        var book = AddBook();

        Assert.Throws<InvalidRequestException>(() => PlaceOrder(0, book.Id));
    }

    [Fact]
    public void GetById_ReturnsCreatedOrder()
    {
        var book = AddBook();
        var order = PlaceOrder(1, book.Id);

        Assert.Equal(order, _orders.GetById(order.Id));
    }

    [Fact]
    public void GetById_Unknown_ThrowsNotFound()
    {
        Assert.Throws<NotFoundException>(() => _orders.GetById(Guid.NewGuid()));
    }

    // ---------- Статус замовлення ----------

    [Fact]
    public void UpdateStatus_ProcessingToShippedToCompleted()
    {
        var order = PlaceOrder(1, AddBook().Id);

        var shipped = _orders.UpdateStatus(order.Id, OrderStatus.Shipped);
        Assert.Equal(OrderStatus.Shipped, shipped.Status);

        var completed = _orders.UpdateStatus(order.Id, OrderStatus.Completed);
        Assert.Equal(OrderStatus.Completed, completed.Status);
        Assert.Equal(OrderStatus.Completed, _orders.GetById(order.Id).Status);
    }

    [Theory]
    [InlineData(OrderStatus.Processing, OrderStatus.Completed)] // пропуск кроку
    [InlineData(OrderStatus.Processing, OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Processing)]   // назад
    [InlineData(OrderStatus.Completed, OrderStatus.Shipped)]    // з фінального статусу
    public void UpdateStatus_InvalidTransition_Throws(OrderStatus from, OrderStatus to)
    {
        var order = PlaceOrder(1, AddBook().Id);
        if (from >= OrderStatus.Shipped) _orders.UpdateStatus(order.Id, OrderStatus.Shipped);
        if (from == OrderStatus.Completed) _orders.UpdateStatus(order.Id, OrderStatus.Completed);

        Assert.Throws<InvalidStatusTransitionException>(() => _orders.UpdateStatus(order.Id, to));
    }

    [Fact]
    public void UpdateStatus_UnknownOrder_ThrowsNotFound()
    {
        Assert.Throws<NotFoundException>(() =>
            _orders.UpdateStatus(Guid.NewGuid(), OrderStatus.Shipped));
    }

    // ---------- Замовлення клієнта ----------

    [Fact]
    public void GetByCustomer_ReturnsOnlyThatCustomersOrders()
    {
        var book = AddBook(qty: 20);
        var o1 = PlaceOrder(1, book.Id);
        var o2 = PlaceOrder(1, book.Id);
        PlaceOrder(2, book.Id);

        var result = _orders.GetByCustomer(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, o => Assert.Equal(1, o.CustomerId));
        Assert.Contains(result, o => o.Id == o1.Id);
        Assert.Contains(result, o => o.Id == o2.Id);
    }

    [Fact]
    public void GetByCustomer_NoOrders_ReturnsEmpty()
    {
        Assert.Empty(_orders.GetByCustomer(42));
    }

    [Fact]
    public void GetByCustomer_IncludesOrdersWithAnyStatus()
    {
        var book = AddBook(qty: 20);
        var done = PlaceOrder(1, book.Id);
        _orders.UpdateStatus(done.Id, OrderStatus.Shipped);
        _orders.UpdateStatus(done.Id, OrderStatus.Completed);
        PlaceOrder(1, book.Id);

        var result = _orders.GetByCustomer(1);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.Status == OrderStatus.Completed);
        Assert.Contains(result, o => o.Status == OrderStatus.Processing);
    }
}
