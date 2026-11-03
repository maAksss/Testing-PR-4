using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookstoreApi.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BookstoreApi.Tests;

/// <summary>Наскрізні тести HTTP-ендпоінтів (новий застосунок для кожного тесту).</summary>
public class ApiIntegrationTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public ApiIntegrationTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<Book> CreateBook(string isbn = "978-0-00", int qty = 5, decimal price = 250m)
    {
        var resp = await _client.PostAsJsonAsync("/books",
            new CreateBookRequest(isbn, "Python", "John Doe", "desc", price, qty), Json);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<Book>(Json))!;
    }

    private async Task<HttpResponseMessage> PlaceOrder(int customerId, int bookId, int qty) =>
        await _client.PostAsJsonAsync("/orders",
            new CreateOrderRequest(customerId, [new OrderItemRequest(bookId, qty)]), Json);

    // ---------- Книги ----------

    [Fact]
    public async Task PostBook_Returns201_WithLocation_AndBody()
    {
        var resp = await _client.PostAsJsonAsync("/books",
            new CreateBookRequest("111", "T", "A", null, 10m, 3), Json);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var book = await resp.Content.ReadFromJsonAsync<Book>(Json);
        Assert.Equal($"/books/{book!.Id}", resp.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task PostBook_DuplicateIsbn_Returns409()
    {
        await CreateBook("dup");

        var resp = await _client.PostAsJsonAsync("/books",
            new CreateBookRequest("dup", "Other", "X", null, 1m, 1), Json);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task PostBook_InvalidData_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/books",
            new CreateBookRequest("", "T", "A", null, 1m, 1), Json);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetBooks_ReturnsAll_AndGetById_ReturnsOne()
    {
        var a = await CreateBook("a");
        await CreateBook("b");

        var all = await _client.GetFromJsonAsync<List<Book>>("/books", Json);
        var one = await _client.GetFromJsonAsync<Book>($"/books/{a.Id}", Json);

        Assert.Equal(2, all!.Count);
        Assert.Equal("a", one!.Isbn);
    }

    [Fact]
    public async Task GetBook_Unknown_Returns404()
    {
        var resp = await _client.GetAsync("/books/12345");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task PutBook_UpdatesPriceAndQuantity()
    {
        var book = await CreateBook();

        var resp = await _client.PutAsJsonAsync($"/books/{book.Id}",
            new UpdateBookRequest(null, null, null, null, 399m, 20), Json);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var updated = await resp.Content.ReadFromJsonAsync<Book>(Json);
        Assert.Equal(399m, updated!.Price);
        Assert.Equal(20, updated.Quantity);
    }

    [Fact]
    public async Task PutBook_ChangingIsbn_Returns400()
    {
        var book = await CreateBook("orig");

        var resp = await _client.PutAsJsonAsync($"/books/{book.Id}",
            new UpdateBookRequest("changed", null, null, null, null, null), Json);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteBook_ById_Returns204_ThenGetReturns404()
    {
        var book = await CreateBook();

        var del = await _client.DeleteAsync($"/books/{book.Id}");
        var get = await _client.GetAsync($"/books/{book.Id}");

        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task DeleteBook_ByIsbn_Returns204()
    {
        await CreateBook("to-delete");

        var del = await _client.DeleteAsync("/books/isbn/to-delete");

        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<List<Book>>("/books", Json))!);
    }

    [Fact]
    public async Task DeleteBook_NonExisting_Returns404_WithoutServerError()
    {
        var resp = await _client.DeleteAsync("/books/9999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ---------- Замовлення ----------

    [Fact]
    public async Task PostOrder_Returns201_AndDecreasesInventory()
    {
        var book = await CreateBook(qty: 5);

        var resp = await PlaceOrder(1, book.Id, 2);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var order = await resp.Content.ReadFromJsonAsync<Order>(Json);
        Assert.Equal(OrderStatus.Processing, order!.Status);
        Assert.Equal(3, (await _client.GetFromJsonAsync<Book>($"/books/{book.Id}", Json))!.Quantity);
    }

    [Fact]
    public async Task PostOrder_MoreThanInStock_Returns409_AndInventoryUnchanged()
    {
        var book = await CreateBook(qty: 2);

        var resp = await PlaceOrder(1, book.Id, 5);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal(2, (await _client.GetFromJsonAsync<Book>($"/books/{book.Id}", Json))!.Quantity);
    }

    [Fact]
    public async Task PostOrder_UnknownBook_Returns404()
    {
        var resp = await PlaceOrder(1, 999, 1);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetOrder_ById_ReturnsOrder_AndUnknownReturns404()
    {
        var book = await CreateBook();
        var order = (await (await PlaceOrder(1, book.Id, 1)).Content.ReadFromJsonAsync<Order>(Json))!;

        var found = await _client.GetFromJsonAsync<Order>($"/orders/{order.Id}", Json);
        var missing = await _client.GetAsync($"/orders/{Guid.NewGuid()}");

        Assert.Equal(order.Id, found!.Id);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task PutOrderStatus_ValidFlow_AndInvalidTransition()
    {
        var book = await CreateBook();
        var order = (await (await PlaceOrder(1, book.Id, 1)).Content.ReadFromJsonAsync<Order>(Json))!;

        var ok = await _client.PutAsJsonAsync($"/orders/{order.Id}/status",
            new UpdateOrderStatusRequest(OrderStatus.Shipped), Json);
        var bad = await _client.PutAsJsonAsync($"/orders/{order.Id}/status",
            new UpdateOrderStatusRequest(OrderStatus.Processing), Json);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(OrderStatus.Shipped, (await ok.Content.ReadFromJsonAsync<Order>(Json))!.Status);
        Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);
    }

    [Fact]
    public async Task GetCustomerOrders_ReturnsOnlyTheirOrders()
    {
        var book = await CreateBook(qty: 10);
        await PlaceOrder(1, book.Id, 1);
        await PlaceOrder(1, book.Id, 1);
        await PlaceOrder(2, book.Id, 1);

        var orders = await _client.GetFromJsonAsync<List<Order>>("/customers/1/orders", Json);

        Assert.Equal(2, orders!.Count);
        Assert.All(orders, o => Assert.Equal(1, o.CustomerId));
    }
}
