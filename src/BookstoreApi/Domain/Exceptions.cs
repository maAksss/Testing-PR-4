namespace BookstoreApi.Domain;

public abstract class DomainException : Exception
{
    public int StatusCode { get; }

    protected DomainException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class InvalidRequestException(string message) : DomainException(message, 400);

public class NotFoundException(string message) : DomainException(message, 404);

public class DuplicateIsbnException(string isbn)
    : DomainException($"Книга з ISBN '{isbn}' вже існує.", 409);

public class IsbnChangeNotAllowedException()
    : DomainException("ISBN змінювати заборонено.", 400);

public class InsufficientStockException(string message) : DomainException(message, 409);

public class InvalidStatusTransitionException(OrderStatus current, OrderStatus target)
    : DomainException($"Неможливо змінити статус з '{current}' на '{target}'.", 409);
