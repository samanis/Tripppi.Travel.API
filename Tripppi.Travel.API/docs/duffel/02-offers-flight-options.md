# Step 2: Offers (Flight Options)

## Overview
After creating an **Offer Request** (Step 1), Duffel returns a list of **Offers** representing available flight options. Each offer is a bookable flight itinerary with pricing, schedule, and fare rules.

---

## Purpose
- Present flight options to the user
- Display pricing, airlines, duration, layovers
- Allow user to compare and select their preferred option
- **No API call required** (offers returned in Step 1 response)

---

## Offer Structure

### Top-Level Offer Fields

```json
{
  "id": "off_0000AZnKr4GuT8xqTLXw6C",
  "total_amount": "350.00",
  "total_currency": "USD",
  "tax_amount": "45.20",
  "tax_currency": "USD",
  "base_amount": "304.80",
  "base_currency": "USD",
  "expires_at": "2024-01-15T10:45:00Z",
  "payment_requirements": {
    "requires_instant_payment": false,
    "payment_required_by": "2024-01-15T23:59:59Z",
    "price_guarantee_expires_at": "2024-01-15T10:45:00Z"
  },
  "slices": [ /* flight segments */ ],
  "passengers": [ /* per-passenger details */ ],
  "conditions": { /* fare rules */ },
  "owner": {
    "iata_code": "AA",
    "name": "American Airlines",
    "logo_symbol_url": "https://..."
  }
}
```

---

## Key Offer Fields

### 1. Pricing

| Field | Type | Description | Display? |
|-------|------|-------------|----------|
| `total_amount` | string | **Total price** (base + taxes + fees) | ? Primary price |
| `total_currency` | string | Currency code (e.g., "USD", "CAD") | ? Yes |
| `base_amount` | string | Base fare (before taxes) | ?? Optional breakdown |
| `tax_amount` | string | Total taxes and fees | ?? Optional breakdown |

**Display Example:**
```
Total: $350.00 USD
  Base fare: $304.80
  Taxes & fees: $45.20
```

### 2. Expiration

| Field | Type | Description | Critical? |
|-------|------|-------------|-----------|
| `expires_at` | ISO 8601 | **Offer expires** after this time | ? Yes - check before booking |
| `payment_requirements.price_guarantee_expires_at` | ISO 8601 | Price locked until this time | ? Yes |

**User Experience:**
- Show countdown timer: "Offer expires in 14 minutes"
- Disable "Book" button after expiration
- Prompt user to re-search if expired

### 3. Owner (Airline)

| Field | Type | Description |
|-------|------|-------------|
| `owner.iata_code` | string | Marketing airline (e.g., "AA" for American) |
| `owner.name` | string | Airline full name |
| `owner.logo_symbol_url` | string | Airline logo URL |

---

## Slices (Flight Segments)

Each **slice** represents one leg of the journey (e.g., outbound or return).

### Slice Structure

```json
{
  "slices": [
    {
      "id": "sli_123abc",
      "origin": {
        "iata_code": "YYZ",
        "name": "Toronto Pearson International Airport",
        "city_name": "Toronto",
        "time_zone": "America/Toronto"
      },
      "destination": {
        "iata_code": "JFK",
        "name": "John F. Kennedy International Airport",
        "city_name": "New York",
        "time_zone": "America/New_York"
      },
      "duration": "PT2H30M",
      "segments": [ /* see below */ ]
    }
  ]
}
```

### Key Slice Fields

| Field | Type | Description |
|-------|------|-------------|
| `duration` | ISO 8601 duration | Total travel time (e.g., "PT2H30M" = 2h 30min) |
| `origin.iata_code` | string | Departure airport code |
| `destination.iata_code` | string | Arrival airport code |
| `segments` | array | Individual flights (direct = 1, layover = 2+) |

**Direct vs. Connections:**
- 1 segment = Direct flight
- 2+ segments = Flight with layovers

---

## Segments (Individual Flights)

Each **segment** is a single flight operated by an airline.

### Segment Structure

```json
{
  "segments": [
    {
      "id": "seg_456def",
      "operating_carrier": {
        "iata_code": "AA",
        "name": "American Airlines",
        "logo_symbol_url": "https://..."
      },
      "marketing_carrier": {
        "iata_code": "AA",
        "name": "American Airlines"
      },
      "aircraft": {
        "iata_code": "738",
        "name": "Boeing 737-800"
      },
      "origin": {
        "iata_code": "YYZ",
        "terminal": "3"
      },
      "destination": {
        "iata_code": "JFK",
        "terminal": "8"
      },
      "departing_at": "2024-03-15T08:00:00-05:00",
      "arriving_at": "2024-03-15T11:30:00-05:00",
      "duration": "PT2H30M",
      "distance": "562.00",
      "marketing_carrier_flight_number": "1234"
    }
  ]
}
```

### Key Segment Fields

| Field | Type | Description | Display? |
|-------|------|-------------|----------|
| `departing_at` | ISO 8601 | Departure time (local timezone) | ? Yes |
| `arriving_at` | ISO 8601 | Arrival time (local timezone) | ? Yes |
| `duration` | ISO 8601 | Flight duration | ? Yes |
| `operating_carrier` | object | Airline actually flying the plane | ? Yes |
| `marketing_carrier` | object | Airline selling the ticket | ?? Optional |
| `aircraft.name` | string | Aircraft type | ?? Optional |
| `origin.terminal` | string | Departure terminal | ? Yes (if available) |
| `destination.terminal` | string | Arrival terminal | ? Yes (if available) |

**Codeshare Flights:**
- `marketing_carrier` ? `operating_carrier` ? Codeshare
- Example: "Booked on American (AA), operated by British Airways (BA)"

---

## Passengers (Per-Passenger Details)

Each passenger has individual pricing and baggage allowances.

```json
{
  "passengers": [
    {
      "id": "pas_789ghi",
      "type": "adult",
      "fare_basis_code": "YAP5",
      "cabin_class": "economy",
      "cabin_class_marketing_name": "Main Cabin",
      "baggages": [
        {
          "type": "checked",
          "quantity": 1
        },
        {
          "type": "carry_on",
          "quantity": 1
        }
      ]
    }
  ]
}
```

### Key Passenger Fields

| Field | Type | Description |
|-------|------|-------------|
| `type` | string | `adult`, `child`, `infant_without_seat` |
| `fare_basis_code` | string | Airline fare code (for reference) |
| `cabin_class_marketing_name` | string | Branded cabin (e.g., "Economy Plus") |
| `baggages` | array | Baggage allowances |

### Baggage Types

| Type | Description |
|------|-------------|
| `carry_on` | Cabin baggage (e.g., 1x 7kg bag) |
| `checked` | Hold baggage (e.g., 1x 23kg bag) |

---

## Conditions (Fare Rules)

Fare rules define change/cancellation policies.

```json
{
  "conditions": {
    "change_before_departure": {
      "allowed": true,
      "penalty_amount": "75.00",
      "penalty_currency": "USD"
    },
    "refund_before_departure": {
      "allowed": false
    }
  }
}
```

### Key Conditions

| Field | Description | Display? |
|-------|-------------|----------|
| `change_before_departure.allowed` | Can change flight before departure? | ? Yes |
| `change_before_departure.penalty_amount` | Change fee | ? Yes |
| `refund_before_departure.allowed` | Refundable? | ? Yes |

---

## Display Guidelines for Tripppi

### 1. Offer Card (Summary View)

```
??????????????????????????????????????????????????
? American Airlines                    $350.00   ?
? ???????????????????????????????????????????????
? YYZ ? JFK     08:00 ? 11:30     Direct         ?
? Mar 15        2h 30m               Economy     ?
? ???????????????????????????????????????????????
? ? 1 checked bag   ? Carry-on                  ?
? ? Non-refundable  ? Change fee: $75           ?
? ???????????????????????????????????????????????
? [Select Flight]              Expires in 14 min ?
??????????????????????????????????????????????????
```

### 2. Sorting Options

Allow users to sort by:
- **Price** (default: lowest first)
- **Duration** (shortest first)
- **Departure time** (earliest/latest)
- **Airline**

### 3. Filtering Options

- Direct flights only
- Max number of stops
- Departure time range (morning, afternoon, evening)
- Airlines (checkboxes)
- Max price

---

## Implementation for Tripppi

### 1. Mapping Offers to DTOs

**Never expose Duffel's raw JSON to the frontend!**

```csharp
public class OfferDto
{
    public string Id { get; set; } // off_...
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string AirlineName { get; set; }
    public string AirlineLogoUrl { get; set; }
    public List<SliceDto> Slices { get; set; }
    public BaggageDto Baggage { get; set; }
    public FareConditionsDto Conditions { get; set; }
}

public class SliceDto
{
    public string OriginIata { get; set; }
    public string DestinationIata { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int StopCount { get; set; } // 0 = direct
    public List<SegmentDto> Segments { get; set; }
}
```

### 2. Caching Selected Offer

When user selects an offer, store the **Offer ID** temporarily:

```csharp
// Store in session or distributed cache
var cacheKey = $"selected_offer_{userId}_{sessionId}";
await _cache.SetStringAsync(
    cacheKey,
    offerId,
    new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
    });
```

**Why?**
- User proceeds to passenger details form (next step)
- Need to retrieve offer for repricing (Step 4)
- Offers expire quickly

### 3. Expiration Handling

**Frontend Timer:**
```javascript
// JavaScript countdown timer
const expiresAt = new Date(offer.expires_at);
const now = new Date();
const remaining = expiresAt - now;

if (remaining <= 0) {
  // Disable booking, show "Offer expired" message
}
```

**Backend Check:**
```csharp
var offer = await GetOfferFromCache(offerId);
if (offer.ExpiresAt <= DateTime.UtcNow)
{
    return BadRequest("Offer has expired. Please search again.");
}
```

### 4. Logging (No PII!)

```csharp
_logger.LogInformation(
    "User viewed offer: {OfferId}, Price: {Amount} {Currency}",
    offer.Id,
    offer.TotalAmount,
    offer.TotalCurrency);

// ? NEVER LOG:
// - Full offer JSON (contains fare rules, internal IDs)
```

---

## Round-Trip Offers

For round-trip searches, offers include **two slices**:

```json
{
  "offers": [
    {
      "id": "off_roundtrip",
      "total_amount": "1250.00",
      "slices": [
        {
          "id": "sli_outbound",
          "origin": { "iata_code": "YYZ" },
          "destination": { "iata_code": "LHR" },
          "segments": [ /* YYZ ? LHR */ ]
        },
        {
          "id": "sli_return",
          "origin": { "iata_code": "LHR" },
          "destination": { "iata_code": "YYZ" },
          "segments": [ /* LHR ? YYZ */ ]
        }
      ]
    }
  ]
}
```

**Display:**
```
Outbound: YYZ ? LHR    Jun 1, 08:00 ? 20:30  (7h 30m, Direct)
Return:   LHR ? YYZ    Jun 15, 10:00 ? 13:00 (8h 00m, Direct)
Total: $1,250.00 USD
```

---

## Common Patterns

### 1. No Offers Available

If `offers` array is empty:
```json
{
  "data": {
    "id": "orq_123",
    "offers": []
  }
}
```

**User Experience:**
- Show "No flights found" message
- Suggest alternative dates (±1-3 days)
- Suggest nearby airports

### 2. Mixed Cabin Classes

Some offers mix cabin classes (e.g., economy outbound, business return):
```json
{
  "slices": [
    { "cabin_class": "economy" },
    { "cabin_class": "business" }
  ]
}
```

**Display:** "Mixed cabin: Economy / Business"

### 3. Layovers

Calculate layover duration between segments:
```csharp
var layoverDuration = segment2.DepartingAt - segment1.ArrivingAt;
// Display: "1h 30m layover in ORD"
```

---

## Error Scenarios

### Offer Expired During Selection

**Scenario:** User takes too long to decide, offer expires.

**Handling:**
1. Detect expiration on backend before proceeding to Step 3
2. Return 410 Gone or 404 Not Found
3. Frontend prompts user to re-search

```csharp
if (offer.ExpiresAt <= DateTime.UtcNow)
{
    _logger.LogWarning("Offer expired: {OfferId}", offer.Id);
    return StatusCode(410, new 
    { 
        error = "This offer has expired. Please search again.",
        code = "offer_expired"
    });
}
```

---

## Next Steps

Once user selects an offer:
1. **Store `offer_id`** in cache/session
2. Navigate to **passenger details form**
3. Proceed to **Step 3: Offer Retrieval** (get latest price/availability)

---

## References

- **Duffel API Docs**: https://duffel.com/docs/api/overview/offers
- **Previous Step**: [01-search-offer-request.md](01-search-offer-request.md)
- **Next Step**: [03-offer-single-selection.md](03-offer-single-selection.md)
- **Architecture**: [tripppi_architecture.md](../../tripppi_architecture.md)
