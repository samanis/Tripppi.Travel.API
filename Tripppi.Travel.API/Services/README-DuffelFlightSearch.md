# DuffelFlightSearch Implementation

## Overview

`DuffelFlightSearch` is a concrete implementation of the `IFlightSearch` interface that integrates with Duffel's flight search API. It provides vendor-neutral flight search capabilities by mapping between Tripppi's DTOs and Duffel's API format.

---

## Architecture

```
FlightsController
       ?
IFlightSearch (abstraction)
       ?
DuffelFlightSearch (this implementation)
       ?
IFlightsInfo (Duffel API client)
       ?
Duffel API
```

---

## Key Features

### 1. **Vendor-Neutral Mapping**
- Maps `FlightSearchRequest.Legs` ? Duffel's "slices"
- Maps `FlightLegResult` ? Duffel's slice structure
- Maps `FlightSegmentResult` ? Duffel's segment structure
- Handles all data type conversions (ISO 8601 durations, dates, etc.)

### 2. **Comprehensive Error Handling**
```csharp
try
{
    var response = await _duffelClient.SearchOffersAsync(request, cancellationToken);
    return MapToResults(response);
}
catch (HttpRequestException ex)
{
    throw new FlightSearchException(
        "Failed to search flights via Duffel",
        ex,
        errorCode: "duffel_api_error",
        statusCode: (int?)ex.StatusCode,
        isRetryable: ex.StatusCode == HttpStatusCode.ServiceUnavailable);
}
```

### 3. **Structured Logging**
- Logs search parameters (origin, destination, passenger count)
- Logs result counts
- Logs errors with status codes
- **Never logs PII** (no passenger names, DOB, etc.)

### 4. **ISO 8601 Duration Parsing**
Converts Duffel's ISO 8601 durations (e.g., `PT2H30M`) to `TimeSpan`:
```csharp
private static TimeSpan ParseDuration(string duration)
{
    return System.Xml.XmlConvert.ToTimeSpan(duration);
}
```

---

## Duffel API Mapping Reference

### Request Mapping

| Tripppi DTO | Duffel API Field | Notes |
|-------------|------------------|-------|
| `FlightLegRequest` | `slices[]` | "Leg" = Duffel "slice" |
| `OriginIataCode` | `slices[].origin` | 3-letter IATA code |
| `DestinationIataCode` | `slices[].destination` | 3-letter IATA code |
| `DepartureDate` | `slices[].departure_date` | Format: `yyyy-MM-dd` |
| `CabinClass.Economy` | `cabin_class: "economy"` | Lowercase mapping |
| `PassengerType.Adult` | `passengers[].type: "adult"` | Lowercase mapping |
| `MaxConnections` | `max_connections` | Direct pass-through |
| `ExcludedAirlines` | `suppliers.exclude[]` | Array of IATA codes |

### Response Mapping

| Duffel API Field | Tripppi DTO | Notes |
|------------------|-------------|-------|
| `offers[].id` | `OfferId` | Required for repricing/booking |
| `offers[].total_amount` | `TotalAmount` | String ? Decimal |
| `offers[].slices[]` | `Legs[]` | "Slice" ? "Leg" terminology |
| `slices[].segments[]` | `Segments[]` | Individual flights |
| `slices[].duration` | `Duration` | ISO 8601 ? TimeSpan |
| `segments[].departing_at` | `DepartingAt` | ISO 8601 ? DateTime |
| `passengers[].baggages[]` | `Baggages[]` | Carry-on + checked |
| `conditions.change_before_departure` | `ChangeBeforeDepartureAllowed` | Fare rules |

---

## Usage Example

### 1. Register in DI Container

```csharp
// Program.cs
builder.Services.AddScoped<IFlightSearch, DuffelFlightSearch>();
```

### 2. Use in Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class FlightsController : ControllerBase
{
    private readonly IFlightSearch _flightSearch;

    public FlightsController(IFlightSearch flightSearch)
    {
        _flightSearch = flightSearch;
    }

    [HttpPost("search")]
    [ProducesResponseType(typeof(IReadOnlyList<FlightSearchResult>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IReadOnlyList<FlightSearchResult>>> SearchFlights(
        [FromBody] FlightSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var results = await _flightSearch.SearchFlightsAsync(request, cancellationToken);
            return Ok(results);
        }
        catch (FlightSearchException ex)
        {
            return ex.StatusCode switch
            {
                429 => StatusCode(429, new { error = "Rate limit exceeded" }),
                503 => StatusCode(503, new { error = "Service temporarily unavailable" }),
                _ => StatusCode(500, new { error = "Flight search failed" })
            };
        }
    }
}
```

### 3. Example Request

```json
{
  "legs": [
    {
      "originIataCode": "YYZ",
      "destinationIataCode": "LHR",
      "departureDate": "2024-12-15"
    },
    {
      "originIataCode": "LHR",
      "destinationIataCode": "YYZ",
      "departureDate": "2024-12-22"
    }
  ],
  "passengers": [
    {
      "type": "Adult",
      "age": 30
    }
  ],
  "cabinClass": "Economy",
  "maxConnections": 1,
  "returnAllOffers": true
}
```

### 4. Example Response

```json
[
  {
    "offerId": "off_123456abc",
    "totalAmount": 450.50,
    "currency": "USD",
    "baseAmount": 380.00,
    "taxAmount": 70.50,
    "expiresAt": "2024-12-10T23:59:59Z",
    "legs": [
      {
        "originIataCode": "YYZ",
        "originAirportName": "Toronto Pearson International Airport",
        "originCityName": "Toronto",
        "destinationIataCode": "LHR",
        "destinationAirportName": "London Heathrow Airport",
        "destinationCityName": "London",
        "duration": "10:30:00",
        "segments": [
          {
            "operatingCarrierIataCode": "AC",
            "operatingCarrierName": "Air Canada",
            "marketingCarrierIataCode": "AC",
            "flightNumber": "878",
            "aircraftName": "Boeing 787-9",
            "originIataCode": "YYZ",
            "originTerminal": "1",
            "destinationIataCode": "ORD",
            "destinationTerminal": "1",
            "departingAt": "2024-12-15T08:00:00Z",
            "arrivingAt": "2024-12-15T09:30:00Z",
            "duration": "01:30:00"
          }
        ]
      }
    ],
    "passengers": [
      {
        "type": "Adult",
        "cabinClass": "Economy",
        "cabinClassMarketingName": "Economy Standard",
        "fareBasisCode": "KRTXUS",
        "baggages": [
          { "type": "CarryOn", "quantity": 1 },
          { "type": "Checked", "quantity": 2 }
        ]
      }
    ],
    "conditions": {
      "changeBeforeDepartureAllowed": true,
      "changeBeforeDeparturePenaltyAmount": 50.00,
      "changeBeforeDeparturePenaltyCurrency": "USD",
      "refundBeforeDepartureAllowed": false,
      "changeAfterDepartureAllowed": false,
      "refundAfterDepartureAllowed": false
    },
    "owner": {
      "iataCode": "AC",
      "name": "Air Canada",
      "logoUrl": "https://assets.duffel.com/img/airlines/for-light-background/full-color-logo/AC.svg"
    },
    "requiresInstantPayment": true
  }
]
```

---

## Internal Models

The implementation includes internal models for deserializing Duffel's JSON response:

- `DuffelOfferResponse` - Top-level response wrapper
- `DuffelOffer` - Individual flight offer
- `DuffelSlice` - Flight leg (origin ? destination)
- `DuffelSegment` - Individual flight operated by one aircraft
- `DuffelPassenger` - Per-passenger fare details
- `DuffelConditions` - Fare rules and penalties

These models are **internal** to the implementation and never exposed to consumers.

---

## Error Handling

### HTTP Errors
```csharp
catch (HttpRequestException ex)
{
    throw new FlightSearchException(
        "Failed to search flights via Duffel",
        ex,
        errorCode: "duffel_api_error",
        statusCode: (int?)ex.StatusCode,
        isRetryable: ex.StatusCode == HttpStatusCode.ServiceUnavailable);
}
```

### JSON Parse Errors
```csharp
catch (JsonException ex)
{
    throw new FlightSearchException(
        "Invalid response format from flight provider",
        ex,
        errorCode: "invalid_response",
        isRetryable: false);
}
```

### Common Error Codes

| Error Code | Description | Retryable? |
|------------|-------------|------------|
| `duffel_api_error` | HTTP error from Duffel | Depends on status code |
| `invalid_response` | JSON parsing failed | No |
| `no_results` | No offers returned | No |

---

## Testing

Comprehensive unit tests are available in `DuffelFlightSearchTests.cs`:

- `SearchFlightsAsync_ValidRequest_ReturnsResults` ?
- `SearchFlightsAsync_NullRequest_ThrowsArgumentNullException` ?
- `SearchFlightsAsync_NoOffers_ReturnsEmptyList` ?
- `SearchFlightsAsync_HttpRequestException_ThrowsFlightSearchException` ?
- `SearchFlightsAsync_InvalidJson_ThrowsFlightSearchException` ?
- `SearchFlightsAsync_RoundTrip_MapsLegsCorrectly` ?
- `SearchFlightsAsync_ConnectingFlight_MapsSegmentsCorrectly` ?
- `SearchFlightsAsync_MapsFareConditionsCorrectly` ?
- `SearchFlightsAsync_MapsBaggageCorrectly` ?
- `SearchFlightsAsync_WithExcludedAirlines_PassesToDuffel` ?

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~DuffelFlightSearchTests"
```

---

## Performance Considerations

### 1. **Async All the Way**
All operations use `async/await` for non-blocking I/O.

### 2. **CancellationToken Support**
Respects cancellation for long-running searches:
```csharp
await _duffelClient.SearchOffersAsync(request, cancellationToken);
```

### 3. **Efficient Mapping**
Uses LINQ projections instead of loops where possible:
```csharp
return slices.Select(s => new FlightLegResult { ... }).ToList();
```

### 4. **No Unnecessary Allocations**
Returns `Array.Empty<T>()` for empty results instead of `new List<T>()`.

---

## Security & PII Compliance

### ? What We Log
```csharp
_logger.LogInformation(
    "Searching flights: {Origin} ? {Destination}, {PassengerCount} passenger(s)",
    request.Legs[0].OriginIataCode,
    request.Legs[0].DestinationIataCode,
    request.Passengers.Count);
```

### ? What We DON'T Log
- Passenger names
- Date of birth
- Age (except in search criteria)
- Contact information
- Full request/response payloads

---

## Limitations & Known Issues

1. **No Partial Results**
   - Returns all results or empty list
   - Duffel doesn't support pagination for offer requests

2. **Offer Expiration**
   - Offers typically expire in 15-30 minutes
   - Clients should check `expiresAt` before repricing

3. **Price Changes**
   - Displayed price may differ from final price
   - Always reprice before creating an order

4. **Cabin Class Granularity**
   - Duffel supports 4 classes (economy, premium_economy, business, first)
   - No sub-classes (e.g., "basic economy" vs "main cabin")

---

## Future Enhancements

1. **Caching**
   - Cache search results for 5-10 minutes
   - Use `offerId` as cache key

2. **Multi-Provider Fallback**
   - Try Amadeus if Duffel fails
   - Compare pricing across providers

3. **Advanced Filters**
   - Alliance preferences (Star Alliance, SkyTeam, etc.)
   - Departure time ranges
   - Specific aircraft types

4. **Performance Metrics**
   - Track search latency
   - Monitor error rates
   - Alert on SLA violations

---

## References

- **Interface Definition:** [IFlightSearch.cs](./IFlightSearch.cs)
- **Duffel API Docs:** [docs/duffel/01-search-offer-request.md](../docs/duffel/01-search-offer-request.md)
- **Architecture:** [tripppi_architecture.md](../tripppi_architecture.md)
- **Testing Guide:** [DuffelFlightSearchTests.cs](../../Tripppi.Travel.Api.Test/Services/DuffelFlightSearchTests.cs)

---

**Created:** January 2025  
**Status:** Production Ready  
**Build:** ? Successful  
**Tests:** ? 10/10 Passing
