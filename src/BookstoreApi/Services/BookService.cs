using BookstoreApi.Domain;

namespace BookstoreApi.Services;

public interface IBookService
{
    Book Add(CreateBookRequest request);
    Book Update(int id, UpdateBookRequest request);
    Book GetById(int id);
    IReadOnlyList<Book> GetAll();
    bool Delete(int id);
    bool DeleteByIsbn(string isbn);

    /// <summary>
    /// Атомарно списує книги зі складу. Якщо хоча б однієї позиції
    /// недостатньо — не змінюється нічого.
    /// </summary>
    IReadOnlyList<OrderItem> Reserve(IEnumerable<OrderItemRequest> items);
}

public class BookService : IBookService
{
    private readonly Dictionary<int, Book> _books = new();
    private readonly object _lock = new();
    private int _nextId = 1;

    public Book Add(CreateBookRequest r)
    {
        var isbn = (r.Isbn ?? "").Trim();
        var title = (r.Title ?? "").Trim();
        var author = (r.Author ?? "").Trim();

        if (isbn.Length == 0) throw new InvalidRequestException("ISBN обов'язковий.");
        if (title.Length == 0) throw new InvalidRequestException("Назва обов'язкова.");
        if (author.Length == 0) throw new InvalidRequestException("Автор обов'язковий.");
        if (r.Price < 0) throw new InvalidRequestException("Ціна не може бути від'ємною.");
        if (r.Quantity < 0) throw new InvalidRequestException("Кількість не може бути від'ємною.");

        lock (_lock)
        {
            if (_books.Values.Any(b => b.Isbn == isbn))
                throw new DuplicateIsbnException(isbn);

            var book = new Book(_nextId++, isbn, title, author, r.Description, r.Price, r.Quantity);
            _books[book.Id] = book;
            return book;
        }
    }

    public Book Update(int id, UpdateBookRequest r)
    {
        if (r.Price is < 0) throw new InvalidRequestException("Ціна не може бути від'ємною.");
        if (r.Quantity is < 0) throw new InvalidRequestException("Кількість не може бути від'ємною.");
        if (r.Title is not null && r.Title.Trim().Length == 0)
            throw new InvalidRequestException("Назва не може бути порожньою.");
        if (r.Author is not null && r.Author.Trim().Length == 0)
            throw new InvalidRequestException("Автор не може бути порожнім.");

        lock (_lock)
        {
            var book = Get(id);

            if (r.Isbn is not null && r.Isbn.Trim() != book.Isbn)
                throw new IsbnChangeNotAllowedException();

            var updated = book with
            {
                Title = r.Title?.Trim() ?? book.Title,
                Author = r.Author?.Trim() ?? book.Author,
                Description = r.Description ?? book.Description,
                Price = r.Price ?? book.Price,
                Quantity = r.Quantity ?? book.Quantity
            };
            _books[id] = updated;
            return updated;
        }
    }

    public Book GetById(int id)
    {
        lock (_lock) return Get(id);
    }

    public IReadOnlyList<Book> GetAll()
    {
        lock (_lock) return _books.Values.OrderBy(b => b.Id).ToList();
    }

    public bool Delete(int id)
    {
        lock (_lock) return _books.Remove(id);
    }

    public bool DeleteByIsbn(string isbn)
    {
        lock (_lock)
        {
            var key = isbn?.Trim();
            var book = _books.Values.FirstOrDefault(b => b.Isbn == key);
            return book is not null && _books.Remove(book.Id);
        }
    }

    public IReadOnlyList<OrderItem> Reserve(IEnumerable<OrderItemRequest> items)
    {
        var grouped = items
            .GroupBy(i => i.BookId)
            .Select(g => (BookId: g.Key, Qty: g.Sum(x => x.Quantity)))
            .ToList();

        lock (_lock)
        {
            // 1) перевірка всіх позицій
            foreach (var (bookId, qty) in grouped)
            {
                var book = Get(bookId);
                if (book.Quantity < qty)
                    throw new InsufficientStockException(
                        $"Недостатньо примірників книги '{book.Title}': запитано {qty}, в наявності {book.Quantity}.");
            }

            // 2) списання
            var result = new List<OrderItem>();
            foreach (var (bookId, qty) in grouped)
            {
                var book = _books[bookId];
                _books[bookId] = book with { Quantity = book.Quantity - qty };
                result.Add(new OrderItem(bookId, qty, book.Price));
            }
            return result;
        }
    }

    private Book Get(int id) =>
        _books.TryGetValue(id, out var book)
            ? book
            : throw new NotFoundException($"Книгу з ID {id} не знайдено.");
}
