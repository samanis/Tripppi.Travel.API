# Step 6: Order Management (Changes & Cancellations)

## Overview
After creating an order (Step 5), passengers may need to modify or cancel their booking. Duffel provides APIs for managing post-booking operations like order changes, cancellations, and retrieving order details.

---

## Operations Covered

1. **Retrieve Order** - Get current order status and details
2. **Order Cancellations** - Cancel a booking (fees apply)
3. **Order Changes** - Modify flights (date changes, passenger edits, seat selection)
4. **Order Change Requests** - Request changes from airline

---

## 1. Retrieve Order (GET)

### Purpose
- **View booking details** for customer support
- **Check order status** (confirmed, cancelled, pending)
- **Retrieve PNR and tickets** for display
- **Verify payment status**

### API Endpoint

```http
GET https://api.duffel.com/air/orders/{order_id}
Authorization: Bearer duffel_live_xxxxxxxxxxxxx
```

### Response

```json
{
  "data": {
    "id": "ord_0000AZnKr4GuT8xqTLXw6F",
    "booking_reference": "ABCDEF",
    "created_at": "2024-01-15T11:10:00Z",
    "total_amount": "350.00",
    "total_currency": "USD",
    "slices": [ /* flight segments */ ],
    "passengers": [ /* passenger details */ ],
    "payment_status": {
      "awaiting_payment": false
    },
    "documents": [ /* e-tickets */ ],
    "cancellation": null
  }
}
```

### Implementation

```csharp
[HttpGet("orders/{orderId}")]
[ProducesResponseType(typeof(OrderDto), 200)]
[ProducesResponseType(404)]
public async Task<ActionResult<OrderDto>> GetOrder(
    string orderId,
    CancellationToken cancellationToken)
{
    _logger.LogInformation("Retrieving order: {OrderId}", orderId);
    
    try
    {
        var orderJson = await _flightsInfo.GetOrderAsync(orderId, cancellationToken);
        var order = JsonSerializer.Deserialize<DuffelOrder>(orderJson);
        
        return Ok(MapToDto(order));
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        return NotFound(new { error = "Order not found" });
    }
}
```

---

## 2. Order Cancellations

### Purpose
- **Cancel entire booking** (all passengers, all flights)
- **Get refund amount** (if applicable)
- **Receive cancellation confirmation**

### API Endpoint

```http
POST https://api.duffel.com/air/order_cancellations
Content-Type: application/json
Authorization: Bearer duffel_live_xxxxxxxxxxxxx
```

### Request Payload

```json
{
  "data": {
    "order_id": "ord_0000AZnKr4GuT8xqTLXw6F"
  }
}
```

### Response

```json
{
  "data": {
    "id": "oce_123abc",
    "order_id": "ord_0000AZnKr4GuT8xqTLXw6F",
    "created_at": "2024-01-16T10:00:00Z",
    "expires_at": "2024-01-16T10:05:00Z",
    "refund_amount": "200.00",
    "refund_currency": "USD",
    "refund_to": "original_form_of_payment",
    "confirmed_at": null
  }
}
```

### Key Fields

| Field | Description |
|-------|-------------|
| `refund_amount` | Amount to be refunded (after airline fees) |
| `expires_at` | Cancellation request expires (must confirm) |
| `confirmed_at` | `null` until you confirm cancellation |

### Two-Step Cancellation

1. **Create cancellation request** (shows refund amount)
2. **Confirm cancellation** (finalizes)

```http
POST https://api.duffel.com/air/order_cancellations/{cancellation_id}/actions/confirm
```

### Implementation

```csharp
[HttpPost("orders/{orderId}/cancel")]
[ProducesResponseType(typeof(CancellationDto), 200)]
[ProducesResponseType(404)]
[ProducesResponseType(422)]
public async Task<ActionResult<CancellationDto>> CancelOrder(
    string orderId,
    CancellationToken cancellationToken)
{
    _logger.LogWarning("Cancellation requested for order: {OrderId}", orderId);
    
    try
    {
        // Step 1: Create cancellation request
        var payload = new { data = new { order_id = orderId } };
        var responseJson = await _httpClient.PostAsJsonAsync(
            "https://api.duffel.com/air/order_cancellations",
            payload,
            cancellationToken);
        
        var cancellation = await responseJson.Content.ReadFromJsonAsync<DuffelCancellation>();
        
        // Show user refund amount, require confirmation
        return Ok(new 
        {
            cancellation_id = cancellation.Id,
            refund_amount = cancellation.RefundAmount,
            requires_confirmation = true,
            expires_at = cancellation.ExpiresAt
        });
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        return NotFound(new { error = "Order not found" });
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
    {
        return UnprocessableEntity(new { error = "Order cannot be cancelled" });
    }
}

[HttpPost("cancellations/{cancellationId}/confirm")]
public async Task<ActionResult> ConfirmCancellation(
    string cancellationId,
    CancellationToken cancellationToken)
{
    _logger.LogWarning("Confirming cancellation: {CancellationId}", cancellationId);
    
    var response = await _httpClient.PostAsync(
        $"https://api.duffel.com/air/order_cancellations/{cancellationId}/actions/confirm",
        null,
        cancellationToken);
    
    response.EnsureSuccessStatusCode();
    
    // Update database
    await UpdateOrderStatus(cancellationId, "cancelled");
    
    // Send cancellation confirmation email
    await _emailService.SendCancellationConfirmationAsync(cancellationId);
    
    return Ok(new { message = "Cancellation confirmed" });
}
```

### Cancellation Policies

**Before Departure:**
- Check `conditions.refund_before_departure.allowed`
- Refund = `total_amount - airline_fee`

**After Departure:**
- Most tickets are **non-refundable** after departure
- Check `conditions.refund_after_departure.allowed`

---

## 3. Order Changes

### Purpose
- **Change flight dates**
- **Add/remove passengers** (if allowed)
- **Upgrade cabin class**
- **Modify passenger names** (typos)

### API Endpoint

```http
POST https://api.duffel.com/air/order_change_requests
Content-Type: application/json
Authorization: Bearer duffel_live_xxxxxxxxxxxxx
Idempotency-Key: {unique_uuid}
```

### Request Payload (Date Change Example)

```json
{
  "data": {
    "order_id": "ord_0000AZnKr4GuT8xqTLXw6F",
    "slices": {
      "remove": [
        {
          "slice_id": "sli_original"
        }
      ],
      "add": [
        {
          "cabin_class": "economy",
          "departure_date": "2024-03-20",
          "destination": "JFK",
          "origin": "YYZ"
        }
      ]
    }
  }
}
```

### Response

```json
{
  "data": {
    "id": "ocr_123abc",
    "order_id": "ord_0000AZnKr4GuT8xqTLXw6F",
    "change_total_amount": "75.00",
    "change_total_currency": "USD",
    "new_total_amount": "425.00",
    "slices": [ /* new flight options */ ],
    "expires_at": "2024-01-16T10:15:00Z"
  }
}
```

### Implementation

**For Tripppi MVP:**
- **Defer order changes** to post-MVP
- Direct users to airline website for changes
- Provide PNR and airline contact info

**Future Implementation:**
```csharp
// Similar to repricing flow:
// 1. Create change request
// 2. Show change fee + new total
// 3. User confirms
// 4. Process payment (if additional amount due)
// 5. Confirm change
```

---

## 4. Webhooks (Real-Time Updates)

### Purpose
- **Schedule changes** (airline delays, cancellations)
- **Order updates** (ticketing status)
- **Payment updates** (refunds processed)

### Webhook Events

| Event Type | Description |
|------------|-------------|
| `order.created` | Order confirmed |
| `order.updated` | Flight schedule changed |
| `order.airline_initiated_change` | Airline changed flight time/date |
| `order.cancelled` | Order cancelled |

### Webhook Payload

```json
{
  "id": "wh_evt_123",
  "type": "order.updated",
  "occurred_at": "2024-01-16T10:00:00Z",
  "data": {
    "id": "ord_0000AZnKr4GuT8xqTLXw6F",
    "booking_reference": "ABCDEF",
    "slices": [ /* updated flight times */ ]
  }
}
```

### Implementation

```csharp
[HttpPost("webhooks/duffel")]
[AllowAnonymous]
public async Task<IActionResult> HandleDuffelWebhook(
    [FromBody] JsonDocument payload,
    [FromHeader(Name = "X-Duffel-Signature")] string signature)
{
    // 1. Verify signature (CRITICAL!)
    var isValid = await _flightsInfo.VerifyWebhookSignatureAsync(
        signature,
        await ReadRawBodyAsync(Request));
    
    if (!isValid)
    {
        _logger.LogWarning("Invalid webhook signature");
        return Unauthorized();
    }
    
    // 2. Parse event type
    var eventType = payload.RootElement.GetProperty("type").GetString();
    var orderId = payload.RootElement.GetProperty("data").GetProperty("id").GetString();
    
    _logger.LogInformation("Webhook received: {Type} for order {OrderId}", eventType, orderId);
    
    // 3. Handle event (async background job)
    await _eventQueue.EnqueueAsync(new WebhookEvent
    {
        Type = eventType,
        OrderId = orderId,
        Payload = payload.RootElement.ToString()
    });
    
    // 4. Respond immediately (Duffel expects 2xx within 5 seconds)
    return Ok();
}
```

### Webhook Signature Verification

```csharp
public async Task<bool> VerifyWebhookSignatureAsync(
    string signatureHeader,
    string payload)
{
    // Signature format: t=1234567890,v1=abc123...
    var parts = signatureHeader.Split(',')
        .Select(p => p.Split('='))
        .ToDictionary(p => p[0], p => p[1]);
    
    var timestamp = long.Parse(parts["t"]);
    var signature = parts["v1"];
    
    // Check timestamp (prevent replay attacks)
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    if (Math.Abs(now - timestamp) > 300) // 5 minutes
        return false;
    
    // Compute HMAC
    var secret = _configuration["Duffel:WebhookSecret"];
    var message = $"{timestamp}.{payload}";
    
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
    var computed = BitConverter.ToString(hash).Replace("-", "").ToLower();
    
    // Constant-time comparison
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(computed),
        Encoding.UTF8.GetBytes(signature));
}
```

---

## Logging (No PII!)

```csharp
// ? Safe to log:
_logger.LogInformation(
    "Order cancelled: {OrderId}, Refund: {Amount}",
    orderId,
    refundAmount);

_logger.LogInformation(
    "Webhook received: {EventType}, Order: {OrderId}",
    eventType,
    orderId);

// ? NEVER LOG:
// - Passenger names, emails
// - Full order/webhook payloads (contain PII)
```

---

## Summary

### Key Takeaways

1. **Retrieve orders** anytime via `GET /air/orders/{order_id}`
2. **Cancellations** are two-step: create request ? confirm
3. **Changes** require creating change request + confirmation
4. **Webhooks** provide real-time updates (must verify signature!)
5. **Idempotency keys** required for all write operations

### For Tripppi MVP

**Implement:**
- ? Retrieve order (for magic-link access)
- ? Cancellation flow (with refund calculation)
- ? Webhook handler (schedule changes)

**Defer to Post-MVP:**
- ?? Order changes (complex flow)
- ?? Seat selection
- ?? Ancillary services

---

## References

- **Duffel API Docs**: https://duffel.com/docs/api/overview/orders
- **Webhook Docs**: https://duffel.com/docs/api/overview/webhooks
- **Previous Step**: [05-order-confirmed-booking.md](05-order-confirmed-booking.md)
- **IFlightsInfo Interface**: [IFlightsInfo.cs](../../appService/IFlightsInfo.cs)
- **Architecture**: [tripppi_architecture.md](../../tripppi_architecture.md)
