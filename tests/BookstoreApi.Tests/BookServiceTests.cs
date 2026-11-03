using BookstoreApi.Domain;
using BookstoreApi.Services;

namespace BookstoreApi.Tests;

public class BookServiceTests
{
    private readonly BookService _store = new();

    private static CreateBookRequest NewBook(string isbn = "123456789", int qty = 10, decimal price = 100m) =>
        new(isbn, "Python Programming", "John Doe", "Опис", price, qty);

    // ---------- Створення ----------

    [Fact]
    public void Add_NewBook_IsStoredAndRetrievable()
    {
        var added = _store.Add(NewBook());

        var fromStore = _store.GetById(added.Id);
        Assert.Equal("123456789", fromStore.Isbn);
        Assert.Equal("Python Programming", fromStore.Title);
        Assert.Equal("John Doe", fromStore.Author);
        Assert.Single(_store.GetAll());
    }

    [Fact]
    public void Add_AssignsUniqueIds()
    {
        var a = _store.Add(NewBook("111"));
        var b = _store.Add(NewBook("222"));

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Add_DuplicateIsbn_Throws()
    {
        _store.Add(NewBook("123456789"));

        Assert.Throws<DuplicateIsbnException>(() =>
            _store.Add(new CreateBookRequest("123456789", "Another Book", "Jane Doe", null, 50m, 1)));

        Assert.Single(_store.GetAll());
    }

    [Theory]
    [InlineData("", "T", "A", 10, 1)]
    [InlineData("1", "", "A", 10, 1)]
    [InlineData("1", "T", "", 10, 1)]
    [InlineData("1", "T", "A", -1, 1)]
    [InlineData("1", "T", "A", 10, -1)]
    public void Add_InvalidData_Throws(string isbn, string title, string author, decimal price, int qty)
    {
        Assert.Throws<InvalidRequestException>(() =>
            _store.Add(new CreateBookRequest(isbn, title, author, null, price, qty)));
    }

    // ---------- Отримання ----------

    [Fact]
    public void GetById_NonExisting_ThrowsNotFound()
    {
        Assert.Throws<NotFoundException>(() => _store.GetById(999));
    }

    [Fact]
    public void GetAll_EmptyStore_ReturnsEmptyList()
    {
        Assert.Empty(_store.GetAll());
    }

    // ---------- Оновлення ----------

    [Fact]
    public void Update_ChangesPriceQuantityAndDescription()
    {
        var book = _store.Add(NewBook());

        var updated = _store.Update(book.Id, new UpdateBookRequest(null, null, null, "Новий опис", 250m, 42));

        Assert.Equal(250m, updated.Price);
        Assert.Equal(42, updated.Quantity);
        Assert.Equal("Новий опис", updated.Description);
        Assert.Equal(updated, _store.GetById(book.Id));
    }

    [Fact]
    public void Update_PartialRequest_KeepsOtherFields()
    {
        var book = _store.Add(NewBook());

        var updated = _store.Update(book.Id, new UpdateBookRequest(null, null, null, null, 999m, null));

        Assert.Equal(999m, updated.Price);
        Assert.Equal(book.Title, updated.Title);
        Assert.Equal(book.Quantity, updated.Quantity);
    }

    [Fact]
    public void Update_IsbnChange_Throws()
    {
        var book = _store.Add(NewBook());

        Assert.Throws<IsbnChangeNotAllowedException>(() =>
            _store.Update(book.Id, new UpdateBookRequest("987654321", null, null, null, null, null)));

        Assert.Equal("123456789", _store.GetById(book.Id).Isbn);
    }

    [Fact]
    public void Update_SameIsbn_IsAllowed()
    {
        var book = _store.Add(NewBook());

        var updated = _store.Update(book.Id, new UpdateBookRequest("123456789", null, null, null, 5m, null));

        Assert.Equal(5m, updated.Price);
    }

    [Fact]
    public void Update_NonExistingBook_ThrowsNotFound()
    {
        Assert.Throws<NotFoundException>(() =>
            _store.Update(999, new UpdateBookRequest(null, null, null, null, 1m, null)));
    }

    [Fact]
    public void Update_NegativePrice_Throws()
    {
        var book = _store.Add(NewBook());

        Assert.Throws<InvalidRequestException>(() =>
            _store.Update(book.Id, new UpdateBookRequest(null, null, null, null, -1m, null)));
    }

    // ---------- Видалення ----------

    [Fact]
    public void Delete_ById_RemovesBook()
    {
        var book = _store.Add(NewBook());

        Assert.True(_store.Delete(book.Id));
        Assert.Throws<NotFoundException>(() => _store.GetById(book.Id));
    }

    [Fact]
    public void DeleteByIsbn_RemovesBook()
    {
        _store.Add(NewBook("123456789"));

        Assert.True(_store.DeleteByIsbn("123456789"));
        Assert.Empty(_store.GetAll());
    }

    [Fact]
    public void Delete_NonExistingBook_DoesNotThrowAndReturnsFalse()
    {
        var ex = Record.Exception(() =>
        {
            Assert.False(_store.Delete(999));
            Assert.False(_store.DeleteByIsbn("no-such-isbn"));
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Delete_ThenAddSameIsbn_IsAllowed()
    {
        var book = _store.Add(NewBook("123456789"));
        _store.Delete(book.Id);

        var again = _store.Add(NewBook("123456789"));

        Assert.NotEqual(book.Id, again.Id);
    }
}
