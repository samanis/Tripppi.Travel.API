# Step 5: Order (Confirmed Booking)

## Overview
Creating an **Order** is the final step that confirms the booking with the airline. Once successful, the passenger receives a confirmed PNR (booking reference) and e-tickets.

---

## Purpose
- **Confirm booking** with the airline
- **Generate PNR** (Passenger Name Record / booking reference)
- **Issue e-tickets** (if instant ticketing)
- **Process payment** (via Duffel Payments or balance)
- **Finalize itinerary**

---

## API Endpoint

```http
POST https://api.duffel.com/air/orders
Content-Type: application/json
Authorization: Bearer duffel_live_xxxxxxxxxxxxx
Idempotency-Key: {unique_uuid}
```

### Critical Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Idempotency-Key` | **YES** | UUID to prevent duplicate bookings (most critical!) |
| `Authorization` | Yes | Bearer token with Duffel API key |

---

## Request Payload

```json
{
  "data": {
    "selected_offers": ["off_repriced_123"],
    "payments": [
      {
        "type": "balance",
        "amount": "350.00",
        "currency": "USD"
      }
    ],
    "passengers": [
      {
        "id": "pas_123",
        "given_name": "John",
        "family_name": "Doe",
        "born_on": "1990-05-15",
        "email": "john.doe@example.com",
        "phone_number": "+1234567890",
        "gender": "m",
        "title": "mr"
      }
    ],
    "metadata": {
      "internal_booking_ref": "BOOK_USER123_20240115",
      "user_id": "usr_abc123"
    }
  }
}
```

### Key Fields

| Field | Required | Description |
|-------|----------|-------------|
| `selected_offers` | Yes | **Repriced offer ID** from Step 4 (not original!) |
| `payments` | Yes | Payment method (see Payment section below) |
| `passengers` | Yes | Same passenger details as Step 4 |
| `metadata` | No | Your internal references (returned in webhooks) |

---

## Payment Methods

### Option A: Balance (Pre-funded Account)

```json
{
  "payments": [
    {
      "type": "balance",
      "amount": "350.00",
      "currency": "USD"
    }
  ]
}
```

**How It Works:**
- You pre-deposit funds into Duffel account
- Each booking deducts from balance
- No per-transaction card processing

**For Tripppi:**
- Suitable for **MVP testing** (pre-fund test account)
- **Not recommended for production** (cash flow constraints)

### Option B: Duffel Payments (Recommended)

```json
{
  "payments": [
    {
      "type": "duffel_payments",
      "amount": "350.00",
      "currency": "USD"
    }
  ]
}
```

**How It Works:**
1. Frontend uses Duffel.js SDK to tokenize card
2. Backend receives payment token
3. Pass token to order creation
4. Duffel charges card, holds funds
5. Duffel pays airline, settles with you later

**Benefits:**
- ? **Zero PCI compliance** (card data never touches your servers)
- ? Instant booking confirmation
- ? 3DS handled by Duffel
- ? Fraud protection included

---

## Response

### Success Response (201 Created)

```json
{
  "data": {
    "id": "ord_0000AZnKr4GuT8xqTLXw6F",
    "booking_reference": "ABCDEF",
    "live_mode": false,
    "created_at": "2024-01-15T11:10:00Z",
    "total_amount": "350.00",
    "total_currency": "USD",
    "slices": [ /* flight segments */ ],
    "passengers": [
      {
        "id": "pas_123",
        "given_name": "John",
        "family_name": "Doe",
        "ticket_number": "0012345678901"
      }
    ],
    "payment_status": {
      "awaiting_payment": false,
      "payment_required_by": null,
      "price_guarantee_expires_at": null
    },
    "documents": [
      {
        "type": "electronic_ticket",
        "passenger_id": "pas_123",
        "ticket_number": "0012345678901"
      }
    ],
    "metadata": {
      "internal_booking_ref": "BOOK_USER123_20240115",
      "user_id": "usr_abc123"
    }
  }
}
```

### Critical Response Fields

| Field | Description | Store in DB? |
|-------|-------------|--------------|
| `id` | **Order ID** (e.g., `ord_...`) | ? **YES** - Primary booking reference |
| `booking_reference` | **PNR** (e.g., "ABCDEF") | ? **YES** - Airline confirmation code |
| `passengers[].ticket_number` | E-ticket number | ? YES - For customer support |
| `payment_status.awaiting_payment` | Payment complete? | Check before confirming |
| `metadata` | Your custom data | ? YES - For order lookup |

---

## Database Storage (Per Your Architecture)

### Booking Table

```sql
CREATE TABLE bookings (
    id UUID PRIMARY KEY,
    duffel_order_id TEXT NOT NULL UNIQUE,
    duffel_offer_id TEXT NOT NULL,
    pnr TEXT NOT NULL,
    payment_status TEXT NOT NULL,
    total_amount DECIMAL(10, 2),
    currency TEXT,
    
    -- Minimal PII (allowed)
    passenger_first_name TEXT,
    passenger_last_name TEXT,
    contact_email TEXT,
    contact_phone TEXT,
    
    -- Metadata
    user_id TEXT,
    session_id TEXT,
    created_at TIMESTAMPTZ NOT NULL,
    
    -- Auto-purge transient PII
    temp_dob DATE,
    temp_passport TEXT,
    purge_after TIMESTAMPTZ
);
```

**What to Store:**
- ? Order ID, PNR, payment status
- ? First/last name, email (permanent)
- ?? DOB, passport (encrypted, auto-purge after 7-30 days)
- ? **NEVER:** Card data, raw API responses

---

## Implementation for Tripppi

### Controller Endpoint

```csharp
[HttpPost("orders")]
[ProducesResponseType(typeof(OrderDto), 201)]
[ProducesResponseType(400)]
[ProducesResponseType(422)]
public async Task<ActionResult<OrderDto>> CreateOrder(
    [FromBody] CreateOrderRequest request,
    CancellationToken cancellationToken)
{
    // Generate idempotency key
    var idempotencyKey = Guid.NewGuid().ToString();
    
    _logger.LogInformation(
        "Creating order for offer {OfferId} with key {Key}",
        request.RepricedOfferId,
        idempotencyKey);
    
    try
    {
        // Build Duffel payload
        var duffelPayload = new
        {
            data = new
            {
                selected_offers = new[] { request.RepricedOfferId },
                payments = new[]
                {
                    new
                    {
                        type = "balance", // Or "duffel_payments"
                        amount = request.TotalAmount,
                        currency = request.Currency
                    }
                },
                passengers = request.Passengers.Select(p => new
                {
                    id = p.PassengerId,
                    given_name = p.GivenName,
                    family_name = p.FamilyName,
                    born_on = p.BornOn.ToString("yyyy-MM-dd"),
                    email = p.Email,
                    phone_number = p.PhoneNumber,
                    gender = p.Gender,
                    title = p.Title
                }),
                metadata = new
                {
                    internal_booking_ref = $"BOOK_{GetUserId()}_{DateTime.UtcNow:yyyyMMddHHmmss}",
                    user_id = GetUserId()
                }
            }
        };
        
        // Call Duffel via IFlightsInfo
        var responseJson = await _flightsInfo.CreateOrderAsync(
            duffelPayload,
            idempotencyKey,
            cancellationToken);
        
        // Parse response
        var order = JsonSerializer.Deserialize<DuffelOrder>(responseJson);
        
        // Store in database
        await StoreBooking(order, idempotencyKey);
        
        // Send confirmation email (background job)
        await _emailService.SendBookingConfirmationAsync(order.Id);
        
        return CreatedAtAction(
            nameof(GetOrder),
            new { orderId = order.Id },
            MapToDto(order));
    }
    catch (DuffelApiException ex)
    {
        _logger.LogError(ex, "Order creation failed");
        
        return ex.StatusCode switch
        {
            HttpStatusCode.UnprocessableEntity => UnprocessableEntity(new { error = ex.Message }),
            _ => StatusCode(500, new { error = "Booking failed. Please try again." })
        };
    }
}
```

---

## Error Handling

### Common Errors

| HTTP Code | Error | Description | Action |
|-----------|-------|-------------|--------|
| 400 | `validation_error` | Invalid request data | Fix payload |
| 422 | `airline_error` | Airline rejected booking | Show airline message |
| 422 | `offer_no_longer_available` | Repriced offer expired | Re-reprice (Step 4) |
| 409 | `conflict` | Idempotency key conflict | Generate new key |
| 500 | `internal_server_error` | Duffel/airline system issue | Retry with **same key** |

### Retryable Errors

**Network timeout or 500 error:**
```csharp
var maxRetries = 3;
var delay = TimeSpan.FromSeconds(2);

for (int i = 0; i < maxRetries; i++)
{
    try
    {
        var response = await _flightsInfo.CreateOrderAsync(
            payload,
            idempotencyKey, // ? SAME KEY for retries
            cancellationToken);
        
        return response;
    }
    catch (HttpRequestException ex) when (i < maxRetries - 1)
    {
        _logger.LogWarning("Order creation failed, retry {Attempt}/{Max}", i + 1, maxRetries);
        await Task.Delay(delay * (i + 1), cancellationToken); // Exponential backoff
    }
}
```

---

## Logging (No PII!)

```csharp
// ? Safe to log:
_logger.LogInformation(
    "Order created: {OrderId}, PNR: {PNR}, Amount: {Amount}",
    order.Id,
    order.BookingReference,
    order.TotalAmount);

// ? NEVER LOG:
// - Passenger names, DOB, passport
// - Full order JSON (contains PII)
// - Payment details
```

---

## Next Steps

After successful order creation:
1. **Store order in database**
2. **Send confirmation email** to passenger
3. **Generate booking retrieval link** (magic link)
4. **Monitor webhooks** for updates (schedule changes, cancellations)

---

## References

- **Duffel API Docs**: https://duffel.com/docs/api/overview/orders
- **Previous Step**: [04-partial-offer-request-reprice.md](04-partial-offer-request-reprice.md)
- **Next Step**: [06-order-management-changes-cancellations.md](06-order-management-changes-cancellations.md)
- **IFlightsInfo Interface**: [IFlightsInfo.cs](../../appService/IFlightsInfo.cs)
- **Architecture**: [tripppi_architecture.md](../../tripppi_architecture.md)
