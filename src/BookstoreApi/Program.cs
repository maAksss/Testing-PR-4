using System.Text.Json.Serialization;
using BookstoreApi.Domain;
using BookstoreApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Для хостингів, що передають порт через змінну середовища (Heroku, Render, Docker)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IBookService, BookService>();
builder.Services.AddSingleton<IOrderService, OrderService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Перетворення доменних винятків на HTTP-відповіді
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (DomainException ex)
    {
        ctx.Response.StatusCode = ex.StatusCode;
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

// ---------- Інвентар ----------
var books = app.MapGroup("/books").WithTags("Books");

books.MapPost("/", (CreateBookRequest request, IBookService svc) =>
{
    var book = svc.Add(request);
    return Results.Created($"/books/{book.Id}", book);
});

books.MapGet("/", (IBookService svc) => Results.Ok(svc.GetAll()));

books.MapGet("/{id:int}", (int id, IBookService svc) => Results.Ok(svc.GetById(id)));

books.MapPut("/{id:int}", (int id, UpdateBookRequest request, IBookService svc) =>
    Results.Ok(svc.Update(id, request)));

books.MapDelete("/{id:int}", (int id, IBookService svc) =>
    svc.Delete(id) ? Results.NoContent() : Results.NotFound(new { error = $"Книгу з ID {id} не знайдено." }));

books.MapDelete("/isbn/{isbn}", (string isbn, IBookService svc) =>
    svc.DeleteByIsbn(isbn) ? Results.NoContent() : Results.NotFound(new { error = $"Книгу з ISBN '{isbn}' не знайдено." }));

// ---------- Замовлення ----------
var orders = app.MapGroup("/orders").WithTags("Orders");

orders.MapPost("/", (CreateOrderRequest request, IOrderService svc) =>
{
    var order = svc.Create(request);
    return Results.Created($"/orders/{order.Id}", order);
});

orders.MapGet("/{id:guid}", (Guid id, IOrderService svc) => Results.Ok(svc.GetById(id)));

orders.MapPut("/{id:guid}/status", (Guid id, UpdateOrderStatusRequest request, IOrderService svc) =>
    Results.Ok(svc.UpdateStatus(id, request.Status)));

// ---------- Замовлення клієнта ----------
app.MapGet("/customers/{customerId:int}/orders", (int customerId, IOrderService svc) =>
    Results.Ok(svc.GetByCustomer(customerId))).WithTags("Customers");

app.Run();

// Потрібно для WebApplicationFactory<Program> в інтеграційних тестах
public partial class Program { }
