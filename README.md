# Currency Conversion API

## Overview
The **Currency Conversion API** provides functionalities for retrieving the latest exchange rates, converting currencies, and fetching historical exchange rates with pagination. The service integrates with the **Frankfurter API** and includes features such as caching, resilience policies, logging, and security.

## Features
### 1. Resilience & Performance
- **Caching**: Reduces the number of API calls by storing exchange rates temporarily.
- **Retry Policies with Exponential Backoff**: Handles intermittent failures.
- **Circuit Breaker**: Prevents system overload during API outages.

### 2. Extensibility & Maintainability
- **Dependency Injection**: Uses interfaces to decouple service implementations.
- **Factory Pattern**: Allows for dynamic selection of currency providers.
- **Future Integration Support**: Designed to integrate multiple exchange rate providers.

### 3. Security & Access Control
- **JWT Authentication**: Ensures secure API access.
- **Role-Based Access Control (RBAC)**: Restricts endpoint usage based on roles.
- **API Throttling**: Prevents abuse by limiting request rates.

### 4. Logging & Monitoring
- **Structured Logging (Serilog)**: Logs details including client IP, request method, and response time.
- **Distributed Tracing (OpenTelemetry)**: Enables monitoring of request flow.
- **External API Request Logging**: Tracks interactions with the Frankfurter API.

## Endpoints
### 1. Retrieve Latest Exchange Rates
```http
GET /api/currency/latest?base=EUR
```
**Response:**
```json
{
  "IsSuccess": true,
  "StatusCode": 200,
  "Data": { "rates": { "USD": 1.12, "GBP": 0.85 } }
}
```

### 2. Currency Conversion
```http
GET /api/currency/convert?from=EUR&to=USD&amount=100
```
**Response:**
```json
{
  "IsSuccess": true,
  "StatusCode": 200,
  "Data": {
    "from": "EUR",
    "to": "USD",
    "amount": 100,
    "convertedAmount": 112,
    "exchangeRate": 1.12
  }
}
```

### 3. Historical Exchange Rates with Pagination
```http
GET /api/currency/historical?base=EUR&startDate=2023-01-01&endDate=2023-01-31&page=1&pageSize=10
```
**Response:**
```json
{
  "IsSuccess": true,
  "StatusCode": 200,
  "Data": { "rates": { "2023-01-01": { "USD": 1.10 }, "2023-01-02": { "USD": 1.11 } } }
}
```

## Installation & Setup
### 1. Prerequisites
- .NET 7 or later
- Serilog & OpenTelemetry dependencies

### 2. Configuration
Set up the **appsettings.json** file with:
```json
{
  "Jwt": { "Secret": "your_secret_key" },
  "AllowedHosts": "*"
}
```

### 3. Run the API
```sh
dotnet run
```

## Technologies Used
- **ASP.NET Core** for API development
- **Frankfurter API** for currency exchange rates
- **Polly** for resilience (retry & circuit breaker)
- **IMemoryCache** for caching
- **Serilog** for structured logging
- **OpenTelemetry** for distributed tracing
- **JWT Authentication** for security

## Contributing
1. Fork the repository
2. Create a new branch
3. Commit your changes
4. Open a Pull Request

## License
This project is licensed under the MIT License.

