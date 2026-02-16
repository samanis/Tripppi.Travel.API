# Step 1: Search (Offer Request)

## Overview
The **Offer Request** is the starting point of the Duffel booking flow. It searches for available flight options based on user criteria (origin, destination, dates, passengers).

---

## Purpose
- Search for flights across multiple airlines
- Get a list of available offers (flight options)
- No commitment or payment at this stage

---

## API Endpoint

```http
POST https://api.duffel.com/air/offer_requests
Content-Type: application/json
Authorization: Bearer duffel_live_xxxxxxxxxxxxx
```

---

## Request Payload

### Required Fields

```json
{
  "data": {
    "cabin_class": "economy",
    "passengers": [
      {
        "type": "adult"
      }
    ],
    "slices": [
      {
        "origin": "YYZ",
        "destination": "JFK",
        "departure_date": "2024-03-15"
      }
    ]
  }
}
```

### Field Descriptions

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `cabin_class` | string | Yes | `economy`, `premium_economy`, `business`, `first` |
| `passengers` | array | Yes | List of passenger types (`adult`, `child`, `infant_without_seat`) |
| `slices` | array | Yes | Flight segments (one-way = 1 slice, round-trip = 2 slices) |
| `slices[].origin` | string | Yes | 3-letter IATA airport code (e.g., "YYZ") |
| `slices[].destination` | string | Yes | 3-letter IATA airport code (e.g., "JFK") |
| `slices[].departure_date` | string | Yes | ISO 8601 date format (YYYY-MM-DD) |

### Optional Fields

```json
{
  "data": {
    "max_connections": 1,
    "return_offers": true,
    "private_fares": {
      "corporate_codes": ["CORP123"]
    },
    "suppliers": {
      "exclude": ["american_airlines"]
    }
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `max_connections` | integer | Maximum layovers (0 = direct only, 1 = max 1 stop) |
| `return_offers` | boolean | If true, waits for all offers before returning |
| `private_fares` | object | Corporate discount codes |
| `suppliers.exclude` | array | Airlines to exclude from results |

---

## Response

### Success Response (202 Accepted)

Duffel returns an **Offer Request ID** and begins searching:

```json
{
  "data": {
    "id": "orq_0000AZnKr4GuT8xqTLXw6A",
    "live_mode": false,
    "slices": [
      {
        "origin": {
          "iata_code": "YYZ",
          "name": "Toronto Pearson International Airport"
        },
        "destination": {
          "iata_code": "JFK",
          "name": "John F. Kennedy International Airport"
        },
        "departure_date": "2024-03-15"
      }
    ],
    "passengers": [
      {
        "id": "pas_0000AZnKr4GuT8xqTLXw6B",
        "type": "adult"
      }
    ],
    "cabin_class": "economy",
    "created_at": "2024-01-15T10:30:00Z",
    "offers": []
  }
}
```

### Key Response Fields

| Field | Description | Action Required |
|-------|-------------|-----------------|
| `id` | Offer Request ID (e.g., `orq_...`) | **Store temporarily** (session/cache, 15-30 min TTL) |
| `offers` | Array of flight offers | Empty initially; poll or use `return_offers: true` |
| `live_mode` | `true` for production, `false` for test | Verify environment matches your API key |

---

## Retrieving Offers

### Option A: Polling (Default Behavior)

After creating the offer request, poll for results:

```http
GET https://api.duffel.com/air/offer_requests/{offer_request_id}?return_offers=true
Authorization: Bearer duffel_live_xxxxxxxxxxxxx
```

**Polling Strategy:**
```
Initial delay: 2 seconds
Retry interval: 1-2 seconds
Max retries: 10
Timeout: 30 seconds
```

### Option B: Synchronous Wait (`return_offers: true`)

Set `return_offers: true` in the initial POST request. Duffel will:
- Wait for offers to populate (up to 30 seconds)
- Return complete offer list immediately
- **Recommended for Tripppi** (simpler UX)

---

## Response with Offers

Once offers are ready, the response includes a populated `offers` array:

```json
{
  "data": {
    "id": "orq_0000AZnKr4GuT8xqTLXw6A",
    "offers": [
      {
        "id": "off_0000AZnKr4GuT8xqTLXw6C",
        "total_amount": "350.00",
        "total_currency": "USD",
        "slices": [
          {
            "duration": "PT2H30M",
            "segments": [
              {
                "operating_carrier": {
                  "iata_code": "AA",
                  "name": "American Airlines"
                },
                "aircraft": {
                  "name": "Boeing 737-800"
                },
                "departing_at": "2024-03-15T08:00:00-05:00",
                "arriving_at": "2024-03-15T11:30:00-05:00",
                "origin": {
                  "iata_code": "YYZ"
                },
                "destination": {
                  "iata_code": "JFK"
                }
              }
            ]
          }
        ],
        "expires_at": "2024-01-15T10:45:00Z",
        "passengers": [
          {
            "id": "pas_0000AZnKr4GuT8xqTLXw6B",
            "fare_basis_code": "YAP5",
            "baggages": [
              {
                "type": "checked",
                "quantity": 1
              }
            ]
          }
        ]
      }
    ]
  }
}
```

---

## Key Offer Fields

| Field | Type | Description | Store? |
|-------|------|-------------|--------|
| `offers[].id` | string | **Offer ID** (e.g., `off_...`) | ? Yes (temporary, 15-30 min) |
| `offers[].total_amount` | string | Total price (base + taxes) | Display to user |
| `offers[].total_currency` | string | Currency code (USD, CAD, etc.) | Display to user |
| `offers[].expires_at` | ISO 8601 | Offer expiration timestamp | Check before booking |
| `offers[].slices` | array | Flight segments (outbound/return) | Display itinerary |

---

## Error Handling

### Common Errors

| HTTP Code | Error Type | Description | Action |
|-----------|------------|-------------|--------|
| 400 | `validation_error` | Invalid airport code, date format, etc. | Validate input before calling API |
| 401 | `authentication_error` | Invalid or expired API key | Check credentials |
| 422 | `airline_error` | No flights available for route/date | Show "no results" message |
| 429 | `rate_limit_exceeded` | Too many requests | Implement exponential backoff |
| 500 | `internal_server_error` | Duffel system issue | Retry with backoff |

### Example Error Response

```json
{
  "errors": [
    {
      "type": "validation_error",
      "title": "Invalid airport code",
      "message": "The origin airport code 'XYZ' is not valid",
      "code": "invalid_airport_code"
    }
  ]
}
```

---

## Implementation Guidelines for Tripppi

### 1. User Input Validation

**Before calling Duffel:**
- Validate airport IATA codes using your `Airports` table
- Ensure departure date is in the future
- Validate passenger counts (max limits vary by airline)

```csharp
// Example validation
if (departureDate <= DateTime.UtcNow.Date)
    return BadRequest("Departure date must be in the future");

var airportExists = await _airportRepository.GetByIataAsync(originIata);
if (airportExists == null)
    return BadRequest($"Invalid origin airport: {originIata}");
```

### 2. Caching Strategy

**Store in cache/session:**
```csharp
var cacheKey = $"offer_request_{userId}_{searchId}";
await _cache.SetAsync(cacheKey, offerRequestId, TimeSpan.FromMinutes(30));
```

**Why?**
- Offers expire in 15-30 minutes
- User may navigate back to search results
- Avoid redundant API calls

### 3. IFlightsInfo Implementation

```csharp
public async Task<string> SearchOffersAsync(
    object searchRequest, 
    CancellationToken cancellationToken = default)
{
    // searchRequest is a DTO/JsonDocument containing:
    // - origin, destination, dates, passengers, cabin_class
    
    var response = await _httpClient.PostAsJsonAsync(
        "https://api.duffel.com/air/offer_requests",
        searchRequest,
        cancellationToken);
    
    response.EnsureSuccessStatusCode();
    
    var result = await response.Content.ReadAsStringAsync();
    // Return JSON string (untyped per IFlightsInfo contract)
    return result;
}
```

### 4. Logging (No PII!)

```csharp
_logger.LogInformation(
    "Offer request created: {OfferRequestId}, Route: {Origin}-{Destination}",
    offerRequestId,
    request.Origin,
    request.Destination);

// ? NEVER LOG:
// - Passenger names
// - Full request/response payloads
```

---

## Round-Trip Example

### Request
```json
{
  "data": {
    "cabin_class": "economy",
    "passengers": [
      { "type": "adult" },
      { "type": "child", "age": 8 }
    ],
    "slices": [
      {
        "origin": "YYZ",
        "destination": "LHR",
        "departure_date": "2024-06-01"
      },
      {
        "origin": "LHR",
        "destination": "YYZ",
        "departure_date": "2024-06-15"
      }
    ]
  }
}
```

### Response Highlights
```json
{
  "data": {
    "id": "orq_roundtrip123",
    "offers": [
      {
        "id": "off_cheapest",
        "total_amount": "1250.00",
        "slices": [
          { "duration": "PT7H30M" },  // Outbound
          { "duration": "PT8H00M" }   // Return
        ]
      }
    ]
  }
}
```

---

## Next Steps

Once you have offers:
1. **Display to user** (Step 2: View Results)
2. **User selects offer** ? Proceed to **Step 3: Offer Retrieval**
3. **Store `offer_id`** temporarily for checkout flow

---

## References

- **Duffel API Docs**: https://duffel.com/docs/api/overview/offer-requests
- **Architecture**: [tripppi_architecture.md](../../tripppi_architecture.md)
- **IFlightsInfo Interface**: [IFlightsInfo.cs](../../appService/IFlightsInfo.cs)
