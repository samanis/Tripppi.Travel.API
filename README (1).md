# Tripppi Travel API

Backend API for **Tripppi**, a B2C flight search and booking platform built on the [Duffel](https://duffel.com) flight API.

Tripppi is a solo side project. The longer-term goal is to turn flight booking into a conversation, where travellers talk to a personal travel assistant instead of filling in search forms. The current focus is a clean, well-tested backend for airport search and flight search that the assistant will build on.

## Features

- **Airport search:** `GET /api/airports/search?q=...` matching airport, city, and country names, with popularity ranking and an in-memory cache preloaded at startup.
- **Flight search:** `POST /api/flights/search` for one-way, round-trip, and multi-leg searches with passengers, cabin class, and connection limits. Results include fares, baggage allowances, and fare conditions.
- **Vendor-neutral abstraction:** controllers depend on `IFlightSearch`, not on Duffel, so other providers (such as Amadeus or Sabre) can be added without changing business logic.
- **Duffel client:** offer requests, partial offer requests, and order creation and retrieval are implemented in `DuffelApiClient`. Booking endpoints are not exposed yet.
- **Health checks:** `GET /api/health`, `/api/health/live`, and `/api/health/ready`.
- **Error handling:** global exception middleware returning RFC 7807 Problem Details.
- **Security:** origin-secret header validation (`X-Origin-Secret`) so only the CDN can reach the API, plus a restricted CORS policy.
- **PII-aware logging:** request and response logging that avoids personal and payment data.

## Tech stack

| Area | Technology |
|---|---|
| Runtime | .NET 10, ASP.NET Core Web API |
| Data | PostgreSQL, Entity Framework Core (Npgsql) |
| Flight data and booking | Duffel API v2 |
| Testing | xUnit, Moq, ASP.NET Core integration tests (`WebApplicationFactory`), EF Core InMemory |
| Logging | AWS CloudWatch (`AWS.Logger.AspNetCore`) |
| Packaging and deployment | Docker, AWS EC2 and RDS deployment scripts (`infra/`) |
| API docs | Swagger / OpenAPI (Development) |

## Architecture

The target production architecture is a modular monolith behind a single public entry point:

```
User
 → CloudFront + WAF (edge)
   ├─ /        → S3 (React SPA)
   └─ /api/*   → ALB → ECS Fargate (this API)
```

Key design decisions:

- **Payments are handled entirely by Duffel.** Tripppi never stores or logs card data.
- **Minimal PII.** Guest checkout only, no user accounts, and transient passenger data is purged after booking.
- **Simple before distributed.** Scale the monolith horizontally before splitting into services.

See [`Tripppi.Travel.API/tripppi_architecture.md`](Tripppi.Travel.API/tripppi_architecture.md) for the full decision record, and [`Tripppi.Travel.API/docs/duffel/`](Tripppi.Travel.API/docs/duffel/) for the Duffel booking flow from search to order management.

## Project structure

```
Tripppi.Travel.API/
  Controllers/      Airports, Flights, Health
  Services/         IFlightSearch, DuffelFlightSearch, DuffelApiClient, LocationService, DTOs
  Repositories/     EF Core airport repository and DbContext
  Middleware/       Exception handling, request logging, origin-secret validation
  Configuration/    Strongly typed Duffel options
  db/               SQL migrations and seed scripts
  docs/             Architecture, Duffel flow, and implementation notes
Tripppi.Travel.Api.Test/
  Controllers/, Services/, Repositories/, Middleware/, Integration/
infra/              EC2, RDS, and CloudWatch deployment scripts
```

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL with the airports database
- A Duffel API key (test keys start with `duffel_test_`)

### Configuration

Set the connection string and Duffel settings using user secrets or environment variables. Do not commit API keys.

```bash
cd Tripppi.Travel.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=AirPath;Username=postgres;Password=<password>"
dotnet user-secrets set "Duffel:ApiKey" "duffel_test_..."
dotnet user-secrets set "Duffel:BaseUrl" "https://api.duffel.com"
```

Apply the scripts in `Tripppi.Travel.API/db/` to add and seed airport popularity scores.

### Run

```bash
dotnet run --project Tripppi.Travel.API
```

The API listens on `http://localhost:5063` (or `https://localhost:7279`). Swagger UI is available at `/swagger` in Development.

### Example requests

```bash
# Airport search
curl "http://localhost:5063/api/airports/search?q=toronto"

# Flight search
curl -X POST "http://localhost:5063/api/flights/search" \
  -H "Content-Type: application/json" \
  -d '{
        "legs": [
          { "originIataCode": "YYZ", "destinationIataCode": "CDG", "departureDate": "2026-12-15" }
        ],
        "passengers": [ { "type": "Adult" } ],
        "cabinClass": "Economy"
      }'
```

### Test

```bash
dotnet test
```

The suite covers controllers, Duffel mapping and search, the airport repository, middleware, and integration tests for airport search.

### Docker

```bash
docker build -f Tripppi.Travel.API/Dockerfile -t tripppi-travel-api .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="..." \
  -e Duffel__ApiKey="duffel_test_..." \
  -e Duffel__BaseUrl="https://api.duffel.com" \
  tripppi-travel-api
```

## Roadmap

- [x] Airport search with caching and popularity ranking
- [x] Flight search through a vendor-neutral abstraction over Duffel
- [ ] Offer selection and repricing endpoints
- [ ] Passenger details and order creation (booking) endpoints
- [ ] Confirmation emails and magic-link booking access
- [ ] **Conversational travel assistant:** a local open-source LLM (via Ollama) that turns requests like "cheapest flight to Paris next Friday" into searches, compares offers, and guides the traveller through booking
- [ ] React front end on S3 and CloudFront

## License

No license has been chosen yet. All rights reserved.
