# Step 3: Offer (Single Selection)

## Overview
After the user selects a flight from the list of offers (Step 2), you must **retrieve the full offer details** from Duffel before proceeding to booking. This ensures the offer is still valid, available, and at the correct price.

---

## Purpose
- **Validate offer still exists** (not expired or sold out)
- **Get latest pricing** (prices can change between search and selection)
- **Retrieve full fare rules** for display during checkout
- **Confirm availability** before collecting passenger details

---

## API Endpoint

```http
GET https://api.duffel.com/air/offers/{offer_id}
Authorization: Bearer duffel_live_xxxxxxxxxxxxx
```

### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `offer_id` | string | Yes | The offer ID from Step 2 (e.g., `off_0000AZnKr4GuT8xqTLXw6C`) |

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `return_available_services` | boolean | false | Include ancillary services (seats, baggage, meals) |

---

## Request Example

```http
GET https://api.duffel.com/air/offers/off_0000AZnKr4GuT8xqTLXw6C
Authorization: Bearer duffel_live_xxxxxxxxxxxxx
```

---

## Response

### Success Response (200 OK)

Returns the full offer object (same structure as Step 2):

```json
{
  "data": {
    "id": "off_0000AZnKr4GuT8xqTLXw6C",
    "total_amount": "350.00",
    "total_currency": "USD",
    "tax_amount": "45.20",
    "base_amount": "304.80",
    "expires_at": "2024-01-15T10:45:00Z",
    "payment_requirements": {
      "requires_instant_payment": false,
      "payment_required_by": "2024-01-15T23:59:59Z",
      "price_guarantee_expires_at": "2024-01-15T10:45:00Z"
    },
    "slices": [ /* flight segments */ ],
    "passengers": [ /* per-passenger details */ ],
    "conditions": {
      "change_before_departure": {
        "allowed": true,
        "penalty_amount": "75.00",
        "penalty_currency": "USD"
      },
      "refund_before_departure": {
        "allowed": false
      }
    },
    "owner": {
      "iata_code": "AA",
      "name": "American Airlines"
    },
    "available_services": [ /* if requested */ ]
  }
}
```

---

## Key Response Fields

### 1. Price Validation

**Critical: Check if price changed!**

```csharp
var cachedPrice = GetCachedPrice(offerId); // From Step 2
var currentPrice = decimal.Parse(offer.TotalAmount);

if (currentPrice != cachedPrice)
{
    _logger.LogWarning(
        "Price changed for offer {OfferId}: {OldPrice} ? {NewPrice}",
        offerId, cachedPrice, currentPrice);
    
    // Notify user of price change
    return Ok(new 
    {
        price_changed = true,
        old_price = cachedPrice,
        new_price = currentPrice,
        offer = MapToDto(offer)
    });
}
```

**User Experience:**
- Show notification: "Price changed from $350 to $365. Continue?"
- Require explicit confirmation before proceeding

### 2. Expiration Check

```csharp
var expiresAt = DateTime.Parse(offer.ExpiresAt);

if (expiresAt <= DateTime.UtcNow)
{
    return StatusCode(410, new 
    { 
        error = "Offer has expired",
        code = "offer_expired"
    });
}

// Calculate time remaining
var timeRemaining = expiresAt - DateTime.UtcNow;
_logger.LogInformation(
    "Offer {OfferId} expires in {Minutes} minutes",
    offerId, timeRemaining.TotalMinutes);
```

### 3. Availability Confirmation

**Response includes availability status:**

| Field | Value | Meaning |
|-------|-------|---------|
| `available` | `true` | Offer can be booked |
| `available` | `false` | Sold out or no longer available |

If `available: false`:
```json
{
  "data": {
    "id": "off_123",
    "available": false,
    "total_amount": null
  }
}
```

**Handling:**
```csharp
if (offer.Available == false)
{
    return StatusCode(410, new 
    { 
        error = "This flight is no longer available",
        code = "offer_unavailable"
    });
}
```

---

## Conditions (Fare Rules)

### Change Policy

```json
{
  "conditions": {
    "change_before_departure": {
      "allowed": true,
      "penalty_amount": "75.00",
      "penalty_currency": "USD"
    },
    "change_after_departure": {
      "allowed": false
    }
  }
}
```

**Display to User:**
```
? Changes allowed before departure ($75 fee)
? Changes not allowed after departure
```

### Refund Policy

```json
{
  "conditions": {
    "refund_before_departure": {
      "allowed": false
    }
  }
}
```

**Display:**
```
? Non-refundable
```

---

## Available Services (Optional)

If you include `?return_available_services=true`, Duffel returns purchasable add-ons:

### Example Response

```json
{
  "data": {
    "id": "off_123",
    "available_services": [
      {
        "id": "ase_seat_selection",
        "type": "seat",
        "total_amount": "25.00",
        "total_currency": "USD",
        "metadata": {
          "seat_number": "12A",
          "seat_type": "window"
        }
      },
      {
        "id": "ase_extra_baggage",
        "type": "baggage",
        "total_amount": "50.00",
        "total_currency": "USD",
        "metadata": {
          "weight": "23kg"
        }
      }
    ]
  }
}
```

### Service Types

| Type | Description | Typical Price |
|------|-------------|---------------|
| `seat` | Seat selection | $10-$150 |
| `baggage` | Extra checked bag | $30-$100 |
| `meal` | In-flight meal | $10-$30 |

**For Tripppi MVP:**
- **Skip ancillary services** initially (complexity)
- Focus on core booking flow
- Add seat selection in later iteration

---

## Error Handling

### Common Errors

| HTTP Code | Error Type | Description | Action |
|-----------|------------|-------------|--------|
| 404 | `not_found` | Offer doesn't exist or expired | Prompt user to re-search |
| 410 | `gone` | Offer explicitly expired | Show "Offer expired" message |
| 422 | `offer_no_longer_available` | Sold out or withdrawn | Suggest alternative flights |
| 500 | `internal_server_error` | Duffel system issue | Retry with exponential backoff |

### Example Error Response

```json
{
  "errors": [
    {
      "type": "offer_no_longer_available",
      "title": "Offer no longer available",
      "message": "This offer is no longer available for booking",
      "code": "offer_unavailable"
    }
  ]
}
```

### Handling Offer Not Found

```csharp
try
{
    var offer = await _duffelClient.GetOfferAsync(offerId);
    return Ok(MapToDto(offer));
}
catch (DuffelApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    _logger.LogWarning("Offer not found: {OfferId}", offerId);
    
    return NotFound(new 
    {
        error = "This offer is no longer available. Please search again.",
        code = "offer_not_found"
    });
}
```

---

## Implementation for Tripppi

### 1. Controller Endpoint

```csharp
[HttpGet("offers/{offerId}")]
[ProducesResponseType(typeof(OfferDetailsDto), 200)]
[ProducesResponseType(404)]
[ProducesResponseType(410)]
public async Task<ActionResult<OfferDetailsDto>> GetOffer(
    string offerId,
    CancellationToken cancellationToken)
{
    // Validate offer ID format
    if (!offerId.StartsWith("off_"))
    {
        return BadRequest(new { error = "Invalid offer ID format" });
    }
    
    _logger.LogInformation("Retrieving offer: {OfferId}", offerId);
    
    try
    {
        // Call Duffel via IFlightsInfo
        var offerJson = await _flightsInfo.GetOfferAsync(offerId, cancellationToken);
        
        // Parse and map to DTO
        var offer = JsonSerializer.Deserialize<DuffelOffer>(offerJson);
        
        // Check expiration
        if (offer.ExpiresAt <= DateTime.UtcNow)
        {
            return StatusCode(410, new { error = "Offer expired" });
        }
        
        // Check availability
        if (offer.Available == false)
        {
            return StatusCode(410, new { error = "Offer no longer available" });
        }
        
        // Map to DTO (hide internal fields)
        var dto = MapOfferToDto(offer);
        
        return Ok(dto);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        _logger.LogWarning("Offer not found: {OfferId}", offerId);
        return NotFound(new { error = "Offer not found" });
    }
}
```

### 2. IFlightsInfo Implementation

```csharp
public async Task<string> GetOfferAsync(
    string offerId, 
    CancellationToken cancellationToken = default)
{
    var response = await _httpClient.GetAsync(
        $"https://api.duffel.com/air/offers/{offerId}",
        cancellationToken);
    
    if (response.StatusCode == HttpStatusCode.NotFound)
    {
        throw new HttpRequestException("Offer not found", null, HttpStatusCode.NotFound);
    }
    
    response.EnsureSuccessStatusCode();
    
    return await response.Content.ReadAsStringAsync(cancellationToken);
}
```

### 3. Caching Strategy

**Cache the offer for checkout:**

```csharp
// After successful retrieval
var cacheKey = $"offer_details_{userId}_{offerId}";
await _cache.SetStringAsync(
    cacheKey,
    offerJson,
    new DistributedCacheEntryOptions
    {
        // Cache until offer expires
        AbsoluteExpiration = offer.ExpiresAt
    });
```

**Why cache?**
- Avoid redundant API calls during checkout
- Faster page loads
- Ensure consistent pricing during booking flow

---

## User Flow Integration

### Typical Checkout Flow

```
Step 2: User clicks "Select Flight"
        ?
Step 3: Frontend calls GET /api/offers/{offer_id}
        ?
Backend: Retrieve offer from Duffel
        ?
Backend: Validate price, expiration, availability
        ?
        ?? Price changed? ? Show notification
        ?? Expired? ? Return 410, prompt re-search
        ?? Available? ? Proceed
        ?
Frontend: Display offer details + passenger form
        ?
Step 4: User enters passenger details ? Reprice
```

---

## Display Offer Details

### Recommended UI Components

```
????????????????????????????????????????????????????
? Flight Details                                   ?
????????????????????????????????????????????????????
? American Airlines                      $350.00   ?
?                                                   ?
? Outbound: Mar 15, 2024                           ?
? YYZ (Toronto) ? JFK (New York)                   ?
? 08:00 ? 11:30 (2h 30m, Direct)                   ?
?                                                   ?
? Baggage:                                          ?
? ? 1 carry-on bag included                        ?
? ? 1 checked bag (23kg) included                  ?
?                                                   ?
? Fare Conditions:                                  ?
? ? Changes allowed ($75 fee)                      ?
? ? Non-refundable                                 ?
?                                                   ?
? ? Offer expires in 12 minutes                    ?
????????????????????????????????????????????????????
? [Continue to Passenger Details]                  ?
????????????????????????????????????????????????????
```

---

## Logging (No PII!)

```csharp
_logger.LogInformation(
    "Offer retrieved: {OfferId}, Price: {Amount}, Expires: {ExpiresAt}",
    offer.Id,
    offer.TotalAmount,
    offer.ExpiresAt);

// ? Safe to log:
// - Offer ID
// - Pricing
// - Expiration time
// - Airline name

// ? NEVER LOG:
// - Full offer JSON (contains internal IDs, fare rules)
// - User session data
```

---

## Edge Cases

### 1. Offer Expired Between Steps

**Scenario:** User loaded search results (Step 2), then waited 20 minutes before clicking "Select".

**Handling:**
```csharp
if (offer.ExpiresAt <= DateTime.UtcNow)
{
    return StatusCode(410, new 
    {
        error = "This offer has expired. Please search again.",
        code = "offer_expired",
        // Optionally include original search params for quick re-search
        search_params = GetOriginalSearchParams(offerId)
    });
}
```

### 2. Price Increased

**Scenario:** Price went from $350 to $365 due to high demand.

**Handling:**
```csharp
var priceChange = new
{
    old_price = cachedPrice,
    new_price = currentPrice,
    difference = currentPrice - cachedPrice,
    percent_change = ((currentPrice - cachedPrice) / cachedPrice) * 100
};

_logger.LogWarning(
    "Price increased for {OfferId}: {OldPrice} ? {NewPrice} (+{Diff}%)",
    offerId, cachedPrice, currentPrice, priceChange.percent_change);

// Return special response requiring user confirmation
return Ok(new 
{
    price_changed = true,
    price_change = priceChange,
    offer = MapToDto(offer),
    requires_confirmation = true
});
```

**Frontend:**
```javascript
if (response.price_changed) {
  showModal({
    title: "Price Changed",
    message: `The price has increased from $${response.price_change.old_price} to $${response.price_change.new_price}. Do you want to continue?`,
    actions: ["Continue", "Search Again"]
  });
}
```

### 3. Sold Out Between Steps

**Scenario:** Flight sold out while user was browsing.

**Handling:**
```csharp
if (offer.Available == false)
{
    _logger.LogWarning("Offer sold out: {OfferId}", offerId);
    
    return StatusCode(410, new 
    {
        error = "This flight is no longer available",
        code = "offer_sold_out",
        // Suggest alternative dates
        suggestions: new 
        {
            alternative_dates = GetAlternativeDates(offer.Slices),
            similar_offers = GetSimilarOffers(offer)
        }
    });
}
```

---

## Next Steps

After successfully retrieving the offer:
1. **Display full offer details** to user
2. **Show passenger details form**
3. **User enters passenger info** (names, DOB, etc.)
4. Proceed to **Step 4: Partial Offer Request (Reprice)**

---

## References

- **Duffel API Docs**: https://duffel.com/docs/api/overview/offers
- **Previous Step**: [02-offers-flight-options.md](02-offers-flight-options.md)
- **Next Step**: [04-partial-offer-request-reprice.md](04-partial-offer-request-reprice.md)
- **IFlightsInfo Interface**: [IFlightsInfo.cs](../../appService/IFlightsInfo.cs)
- **Architecture**: [tripppi_architecture.md](../../tripppi_architecture.md)
