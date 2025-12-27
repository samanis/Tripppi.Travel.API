# Copilot Instructions for Tripppi.Travel.API

## Project Overview

**Tripppi** is a B2C flight booking platform (MVP) built as a **modular monolith** with a React SPA frontend and ASP.NET Core 10 backend. See [tripppi_architecture.md](../Tripppi.Travel.API/tripppi_architecture.md) for design philosophy and security model.

### Key Constraints
- **Zero PII storage**: Never persist card data, full DOB, or passport info. Log IDs only, not payloads.
- **Guest checkout only**: No user accounts or passwords. Magic-link email access via short-lived tokens.
- **Idempotency-safe**: All booking operations (reprice, create order) must support idempotency keys.
- **Duffel integration**: All flight/payment operations delegated to Duffel via `IFlightsInfo` abstraction.

---

## Architecture Patterns

### Layered Structure
```
Controllers → AppService interfaces (IFlightsInfo) → Repositories → Domain → Database
```

- **Controllers**: HTTP endpoints only (route marshaling, minimal logic)
- **AppService**: Business logic facades (e.g., `IFlightsInfo` for flight ops)
- **Repositories**: Data access layer via EF Core (e.g., `EfAirportRepository` for read-only airport queries)
- **Domain**: POCO models with data annotations (e.g., `Airport` as `[Keyless]` record)

### Critical Patterns

#### 1. Read-Only Repositories with `AsNoTracking()`
[EfAirportRepository](../Tripppi.Travel.API/Repositories/EfAirportRepository.cs) demonstrates the pattern:
- Always use `AsNoTracking()` for immutable data (airports).
- Use `IReadOnlyList<T>` return types to signal immutability.
- Include explicit null checks and validation (`string.IsNullOrWhiteSpace`).
- Results limited to 200 items (`Take(200)`) for performance.

```csharp
public async Task<IReadOnlyList<Airport>> SearchByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(prefix))
        return Array.Empty<Airport>();
    
    prefix = prefix.Trim();
    var pattern = $"{prefix}%";
    
    return await _db.Airports
        .AsNoTracking()
        .Where(a => EF.Functions.ILike(a.AirportName ?? string.Empty, pattern) || ...)
        .OrderBy(a => a.AirportName)
        .Take(200)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);
}
```

#### 2. Abstraction Over Duffel via `IFlightsInfo`
[IFlightsInfo.cs](../Tripppi.Travel.API/appService/IFlightsInfo.cs) is the **sole external integration point**:
- Untyped `object` payloads for flexibility (callers use DTOs or `JsonDocument`).
- Idempotency keys optional but **required for** `RepriceOfferAsync` and `CreateOrderAsync`.
- Webhook signature verification via `VerifyWebhookSignatureAsync`.
- **Never return raw sensitive data**—only IDs and status codes.

**When adding features:**
- Implement `IFlightsInfo` to delegate to Duffel, **never add business logic there**.
- Controllers/services should marshal payloads to/from `IFlightsInfo` methods.

#### 3. Domain Models as Records with Nullable Properties
[Airport.cs](../Tripppi.Travel.API/domain/Airport.cs) pattern:
- Sealed records for immutable domain objects.
- `[Column]` attributes map to snake_case database columns.
- Reference types nullable (`string?`), numeric types use `double?` for nullability.
- `[Keyless]` for tables without primary keys.

---

## Development Workflows

### Build & Debug
- **Target framework**: .NET 10.0 (also supports net8.0, net9.0 in build matrix)
- **Launch**: `dotnet run` or use launchSettings.json profiles (http/https/Docker)
  - HTTP: `http://localhost:5063`
  - Swagger UI: `http://localhost:5063/swagger`
- **Database**: PostgreSQL via [Npgsql](https://www.npgsql.org/)

### Testing
- **Framework**: xUnit + coverlet
- **Location**: [Tripppi.Travel.Api.Test](../Tripppi.Travel.Api.Test/)
- **Example**: [UnitTest1.cs](../Tripppi.Travel.Api.Test/UnitTest1.cs) (currently a template)
- **Pattern**: Use `CancellationToken` in async tests; verify idempotency logic for Duffel calls.

### Docker
- **Dockerfile**: Multi-stage (build → publish → final) using .NET 8.0 runtime
- **Ports**: 8080 (HTTP), 8081 (HTTPS)
- **Run locally**: From launchSettings.json profile `Container (Dockerfile)` or `docker build .`

---

## Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Npgsql | 10.0.1 | PostgreSQL driver |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.0 | EF Core provider |
| Swashbuckle.AspNetCore | 6.6.2 | Swagger/OpenAPI |

---

## PII & Security Guidelines

### What NOT to Do
- ❌ Store raw card data, CVV, expiry, or billing address
- ❌ Log request/response bodies with PII
- ❌ Persist full DOB or passport numbers long-term
- ❌ Expose Duffel order details in API responses

### What to Do
- ✅ Store only: `offer_id`, `order_id`, PNR, payment status, first/last name, email
- ✅ Encrypt transient PII at rest; auto-purge after 7–30 days
- ✅ Return only IDs and status codes to clients (no passenger details)
- ✅ Include idempotency keys in all Duffel write operations
- ✅ Verify webhook signatures before processing

---

## Common Tasks

### Adding a New Airport Search Endpoint
1. Create controller method in `Controllers/SomeController.cs`
2. Inject `IAirportRepository` from DI
3. Call `repository.SearchByPrefixAsync(prefix, cancellationToken)` or similar
4. Return airport list (IATA, city, country only—no sensitive data)
5. **Do not expose internal domain model directly**—map to a DTO first

### Integrating a Duffel Workflow
1. Add method to `IFlightsInfo` if not already present
2. Implement in concrete provider (e.g., `DuffelApiClient : IFlightsInfo`)
3. Pass `idempotencyKey` for idempotent operations (reprice, create order)
4. Controllers call interface, never the implementation directly
5. Verify webhook signatures before updating order state

### Handling Errors
- Use `CancellationToken` everywhere async; respect cancellation
- Return meaningful HTTP status codes (400 for validation, 500 for system errors)
- **Never log PII**—log error codes and IDs only
- Use `ConfigureAwait(false)` in repository methods (library convention)

---

## File Organization Reference

```
Tripppi.Travel.API/
  Controllers/         # HTTP endpoints only
  appService/          # Facades (e.g., IFlightsInfo)
  domain/              # Domain models (Airport.cs)
  Repositories/
    interfaces/        # Contracts (IAirportRepository)
    *.cs              # Implementations (EfAirportRepository, AppDbContext)
  Properties/          # launchSettings.json
  docs/                # Sequence diagrams, architecture notes
  Dockerfile           # Multi-stage build
  Program.cs           # Startup & DI configuration
  tripppi_architecture.md  # Read this first
```

---

## Quick Reference: Code Patterns

**Async method signature:**
```csharp
public async Task<IReadOnlyList<T>> GetAsync(CancellationToken cancellationToken = default)
{
    return await _db.Set<T>()
        .AsNoTracking()
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);
}
```

**Duffel abstraction call:**
```csharp
var payload = new { /* search params */ };
var result = await _flightsInfo.SearchOffersAsync(payload, cancellationToken);
// Result is untyped—parse as needed
```

**Webhook signature verification:**
```csharp
bool isValid = await _flightsInfo.VerifyWebhookSignatureAsync(headerValue, payloadBody);
if (!isValid) return Unauthorized();
```

---

## Contact & Resources
- **Architecture rationale**: [tripppi_architecture.md](../Tripppi.Travel.API/tripppi_architecture.md)
- **Data flow**: [sequence-diagram.mmd](../Tripppi.Travel.API/docs/sequence-diagram.mmd)
