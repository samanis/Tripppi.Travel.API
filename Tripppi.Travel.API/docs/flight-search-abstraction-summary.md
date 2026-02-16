# Vendor-Neutral Flight Search Abstraction

## ? Implementation Complete

A vendor-neutral abstraction layer has been created for flight search operations in Tripppi.Travel.API. This allows switching between different flight providers (Duffel, Amadeus, Sabre, etc.) without modifying business logic or controllers.

---

## ?? Files Created

| File | Location | Purpose |
|------|----------|---------|
| **IFlightSearch.cs** | `Services/IFlightSearch.cs` | Core interface defining vendor-neutral flight search contract |
| **FlightSearchRequest.cs** | `Services/DTOs/FlightSearchRequest.cs` | Request DTO with search criteria (origin, destination, dates, passengers) |
| **FlightSearchResult.cs** | `Services/DTOs/FlightSearchResult.cs` | Response DTO with flight offers, pricing, and itinerary details |
| **FlightSearchException.cs** | `Services/FlightSearchException.cs` | Custom exception with vendor-agnostic error codes |
| **README-FlightSearch.md** | `Services/README-FlightSearch.md` | Comprehensive documentation and implementation guide |

---

## ?? Key Features

### 1. Vendor-Neutral Interface
```csharp
public interface IFlightSearch
{
    Task<IReadOnlyList<FlightSearchResult>> SearchFlightsAsync(
        FlightSearchRequest request, 
        CancellationToken cancellationToken = default);
}
```

**Benefits:**
- Switch from Duffel to Amadeus without changing controllers
- A/B test multiple providers
- Mock implementations for unit testing

### 2. Comprehensive DTOs

**FlightSearchRequest includes:**
- ? Flight slices (one-way, round-trip)
- ? Passenger details (adults, children, infants)
- ? Cabin class preferences
- ? Max connections filter
- ? Excluded airlines
- ? Corporate discount codes
- ? Complete validation attributes

**FlightSearchResult includes:**
- ? Offer ID (for booking)
- ? Pricing breakdown (base fare, taxes, total)
- ? Flight segments with times, airports, carriers
- ? Baggage allowances per passenger
- ? Fare conditions (change fees, refund policy)
- ? Offer expiration time

### 3. Structured Error Handling

```csharp
throw new FlightSearchException(
    "Search failed",
    errorCode: "rate_limit_exceeded",
    statusCode: 429,
    isRetryable: true);
```

### 4. .NET 10 Best Practices
- ? Sealed records for DTOs (immutability)
- ? Required properties with `init` accessors
- ? `CancellationToken` support
- ? `IReadOnlyList<T>` for results
- ? Complete XML documentation

---

## ??? Architecture

```
???????????????????????????????????????????????????????????
?              FlightsController                          ?
?  + SearchFlights(FlightSearchRequest)                   ?
???????????????????????????????????????????????????????????
                        ?
???????????????????????????????????????????????????????????
?          IFlightSearch (Vendor-Neutral Interface)       ?
?  + SearchFlightsAsync(request, cancellationToken)       ?
???????????????????????????????????????????????????????????
                        ?
        ??????????????????????????????????
        ?                                ?
????????????????????          ????????????????????
? DuffelFlightSearch?          ?AmadeusFlightSearch?
?                   ?          ?                   ?
?  Maps to/from     ?          ?  Maps to/from     ?
?  Duffel API       ?          ?  Amadeus API      ?
????????????????????          ????????????????????
        ?                                ?
????????????????????          ????????????????????
?  IFlightsInfo     ?          ?  Amadeus SDK      ?
?  (Duffel client)  ?          ?                   ?
????????????????????          ????????????????????
```

---

## ?? Next Steps

### Phase 1: Implement Duffel Provider (MVP)

**Task:** Create `DuffelFlightSearch : IFlightSearch`

**Files to create:**
1. `Services/DuffelFlightSearch.cs` - Implementation class
2. `Services/Mappers/DuffelMapper.cs` - DTO mapping logic
3. `Tripppi.Travel.Api.Test/Services/DuffelFlightSearchTests.cs` - Unit tests

**Implementation checklist:**
```csharp
// 1. Create implementation
public sealed class DuffelFlightSearch : IFlightSearch
{
    private readonly IFlightsInfo _duffelClient;
    private readonly ILogger<DuffelFlightSearch> _logger;
    
    public async Task<IReadOnlyList<FlightSearchResult>> SearchFlightsAsync(
        FlightSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        // Map FlightSearchRequest ? Duffel offer request format
        var duffelRequest = MapToDuffelRequest(request);
        
        // Call Duffel API via IFlightsInfo
        var responseJson = await _duffelClient.SearchOffersAsync(
            duffelRequest, 
            cancellationToken);
        
        // Parse and map Duffel response ? FlightSearchResult
        var results = MapToFlightSearchResults(responseJson);
        
        return results;
    }
}

// 2. Register in DI (Program.cs)
builder.Services.AddScoped<IFlightSearch, DuffelFlightSearch>();

// 3. Use in controller
[ApiController]
[Route("api/[controller]")]
public class FlightsController : ControllerBase
{
    private readonly IFlightSearch _flightSearch;
    
    [HttpPost("search")]
    public async Task<ActionResult> Search([FromBody] FlightSearchRequest request)
    {
        var results = await _flightSearch.SearchFlightsAsync(request);
        return Ok(results);
    }
}
```

### Phase 2: Add More Operations

**Extend `IFlightSearch` with:**
```csharp
public interface IFlightSearch
{
    // Existing
    Task<IReadOnlyList<FlightSearchResult>> SearchFlightsAsync(...);
    
    // New operations
    Task<FlightOfferDetails> GetOfferDetailsAsync(
        string offerId, 
        CancellationToken cancellationToken = default);
    
    Task<RepriceResult> RepriceOfferAsync(
        string offerId, 
        List<PassengerDetails> passengers,
        CancellationToken cancellationToken = default);
}
```

### Phase 3: Multi-Provider Support

**Load balancing:**
```csharp
public class MultiProviderFlightSearch : IFlightSearch
{
    private readonly DuffelFlightSearch _duffel;
    private readonly AmadeusFlightSearch _amadeus;
    
    public async Task<IReadOnlyList<FlightSearchResult>> SearchFlightsAsync(...)
    {
        // Try Duffel first
        try { return await _duffel.SearchFlightsAsync(...); }
        catch (FlightSearchException ex) when (ex.IsRetryable)
        {
            // Fallback to Amadeus
            return await _amadeus.SearchFlightsAsync(...);
        }
    }
}
```

---

## ?? Testing Strategy

### Unit Tests

```csharp
public class DuffelFlightSearchTests
{
    [Fact]
    public async Task SearchFlightsAsync_ValidRequest_ReturnsResults()
    {
        // Arrange
        var mockDuffelClient = new Mock<IFlightsInfo>();
        mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), default))
            .ReturnsAsync(GetMockDuffelResponse());
        
        var service = new DuffelFlightSearch(mockDuffelClient.Object, logger);
        
        var request = new FlightSearchRequest
        {
            Slices = new List<FlightSliceRequest>
            {
                new() 
                { 
                    OriginIataCode = "YYZ", 
                    DestinationIataCode = "JFK",
                    DepartureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30))
                }
            },
            Passengers = new List<PassengerRequest>
            {
                new() { Type = PassengerType.Adult }
            },
            CabinClass = CabinClass.Economy
        };
        
        // Act
        var results = await service.SearchFlightsAsync(request);
        
        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.NotNull(r.OfferId));
    }
}
```

### Integration Tests

```csharp
public class FlightsControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task SearchFlights_ValidRequest_Returns200()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new FlightSearchRequest { /* ... */ };
        
        // Act
        var response = await client.PostAsJsonAsync("/api/flights/search", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<FlightSearchResult>>();
        Assert.NotNull(results);
    }
}
```

---

## ?? Security Considerations

### 1. No PII in Logs
```csharp
// ? Good
_logger.LogInformation("Search completed: {Count} results", results.Count);

// ? Bad - exposes passenger data
_logger.LogDebug("Search request: {Request}", request);
```

### 2. Validate All Inputs
DTOs include comprehensive validation:
```csharp
[Required, StringLength(3, MinimumLength = 3)]
[RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "IATA code must be 3 uppercase letters")]
public required string OriginIataCode { get; init; }
```

### 3. Respect Cancellation Tokens
```csharp
public async Task<IReadOnlyList<FlightSearchResult>> SearchFlightsAsync(
    FlightSearchRequest request,
    CancellationToken cancellationToken = default) // ? Always include
{
    // Pass to downstream calls
    await _httpClient.GetAsync(url, cancellationToken);
}
```

---

## ?? Documentation

**Comprehensive guide available:**
`Tripppi.Travel.API/Services/README-FlightSearch.md`

**Includes:**
- Architecture diagrams
- Usage examples
- Implementation guide for providers
- Testing strategies
- Benefits of abstraction
- FAQ

---

## ? Benefits Achieved

### 1. **Vendor Independence**
- Switch from Duffel to Amadeus with zero controller changes
- Test with mock implementations
- Compare pricing across multiple providers

### 2. **Type Safety**
- Strongly-typed DTOs with validation
- Compile-time checks for all fields
- IntelliSense support for developers

### 3. **Testability**
- Mock `IFlightSearch` for unit tests
- No dependency on external APIs during testing
- Integration tests with real providers

### 4. **Maintainability**
- Single interface to document and understand
- Implementation details hidden
- Easy to extend with new operations

### 5. **Performance**
- DTOs optimized for serialization
- No unnecessary data mapping
- Async-first with cancellation support

---

## ?? Ready for Implementation

**All files compile successfully** ?  
**Build status:** Success  
**Target framework:** .NET 10

**Next action:** Create `DuffelFlightSearch` implementation class following the guide in `Services/README-FlightSearch.md`.

---

## ?? Support

For questions about this abstraction:
1. Read `Services/README-FlightSearch.md`
2. Review `docs/duffel/01-search-offer-request.md` for Duffel specifics
3. Check `.github/copilot-instructions.md` for architecture patterns
4. Consult `tripppi_architecture.md` for design philosophy

---

**Created:** January 2024  
**Status:** Ready for implementation  
**Build:** ? Successful
