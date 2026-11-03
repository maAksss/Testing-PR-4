# Практична робота 4 — API книжкового магазину (C# / ASP.NET Core 8)

REST API для керування інвентарем книг, обробки покупок і відстеження замовлень + автоматичні тести (xUnit).
Дані зберігаються в пам'яті (після перезапуску скидаються).

## Структура

```
BookstoreApi.sln
Dockerfile
src/BookstoreApi/            # API
  Program.cs                 # ендпоінти (Minimal API), обробка помилок, Swagger
  Domain/Models.cs           # Book, Order, OrderStatus, DTO
  Domain/Exceptions.cs       # доменні винятки → HTTP-коди
  Services/BookService.cs    # інвентар
  Services/OrderService.cs   # замовлення і статуси
tests/BookstoreApi.Tests/    # тести
  BookServiceTests.cs        # unit: інвентар
  OrderServiceTests.cs       # unit: покупки, статуси, замовлення клієнта
  ApiIntegrationTests.cs     # інтеграційні: HTTP-ендпоінти
postman/Bookstore.postman_collection.json
```

## Вимоги
.NET SDK 8.0+ (https://dotnet.microsoft.com/download)

## Запуск
```bash
dotnet restore
dotnet run --project src/BookstoreApi --urls http://localhost:5000
```
Swagger UI: http://localhost:5000/swagger

## Запуск тестів
```bash
dotnet test
# з покриттям:
dotnet test --collect:"XPlat Code Coverage"
```

## Ендпоінти

| Метод | URL | Опис | Коди |
|---|---|---|---|
| POST | `/books` | Додати книгу | 201, 400, 409 (дубль ISBN) |
| GET | `/books` | Усі книги | 200 |
| GET | `/books/{id}` | Книга за ID | 200, 404 |
| PUT | `/books/{id}` | Оновити (ціна, кількість, опис, назва, автор); ISBN змінювати не можна | 200, 400, 404 |
| DELETE | `/books/{id}` | Видалити за ID | 204, 404 |
| DELETE | `/books/isbn/{isbn}` | Видалити за ISBN | 204, 404 |
| POST | `/orders` | Створити замовлення | 201, 400, 404, 409 (мало книг) |
| GET | `/orders/{id}` | Замовлення за ID (Guid) | 200, 404 |
| PUT | `/orders/{id}/status` | Змінити статус | 200, 404, 409 |
| GET | `/customers/{customerId}/orders` | Замовлення клієнта | 200 |

### Приклади тіл запитів
```json
POST /books
{ "isbn": "978-966-00-0001", "title": "Python Programming", "author": "John Doe",
  "description": "Підручник", "price": 350, "quantity": 10 }

PUT /books/1
{ "price": 399, "quantity": 25 }

POST /orders
{ "customerId": 1, "items": [ { "bookId": 1, "quantity": 2 } ] }

PUT /orders/{id}/status
{ "status": "Shipped" }
```

### Статуси замовлення
`Processing` (на обробці) → `Shipped` (відправлено) → `Completed` (виконано).
Інші переходи заборонені (409).

### Бізнес-правила
- ISBN унікальний; змінити ISBN при оновленні не можна.
- Замовлення атомарне: якщо хоча б для однієї позиції не вистачає примірників, нічого не списується.
- Після успішного замовлення кількість на складі зменшується.
- Видалення неіснуючої книги не викликає винятку: сервіс повертає `false`, API — `404`.

## Postman
Імпортуйте `postman/Bookstore.postman_collection.json` (змінна `baseUrl` = `http://localhost:5000`).
Порядок: POST книга → GET книги → PUT → POST замовлення → GET/PUT замовлення → DELETE.

## Деплой
Локально — див. вище. Heroku офіційно не підтримує .NET без сторонніх buildpack-ів, тому надійніший варіант — Docker:

```bash
docker build -t bookstore-api .
docker run -p 8080:8080 bookstore-api      # http://localhost:8080/swagger
```
Heroku (контейнер):
```bash
heroku login
heroku create
heroku stack:set container
git push heroku master
heroku open
```
Додаток читає порт зі змінної середовища `PORT`, тож підходить також для Render, Railway, Azure App Service.
