# Duffel Integration Guide for Tripppi.Travel.API

## Overview
This directory contains comprehensive documentation for integrating **Duffel** (flight booking API) into the Tripppi B2C flight booking platform. Each document covers a specific step in the booking workflow.

---

## ?? Table of Contents

### [Step 1: Search (Offer Request)](01-search-offer-request.md)
**Purpose:** Search for available flights  
**API Call:** `POST /air/offer_requests`  
**Returns:** List of offers (flight options)  

**Key Concepts:**
- Creating offer requests with origin, destination, dates, passengers
- Handling round-trip vs. one-way searches
- Polling for results vs. synchronous wait
- Caching offer request IDs

---

### [Step 2: Offers (Flight Options)](02-offers-flight-options.md)
**Purpose:** Display search results to user  
**API Call:** None (uses data from Step 1)  
**Returns:** N/A (client-side processing)  

**Key Concepts:**
- Understanding offer structure (slices, segments, passengers)
- Pricing breakdown (base fare, taxes, total)
- Expiration handling
- Sorting and filtering options
- Mapping to DTOs (no raw API exposure)

---

### [Step 3: Offer (Single Selection)](03-offer-single-selection.md)
**Purpose:** Validate selected offer before checkout  
**API Call:** `GET /air/offers/{offer_id}`  
**Returns:** Full offer details with latest pricing  

**Key Concepts:**
- Checking if offer still available
- Detecting price changes
- Validating expiration
- Caching for checkout flow

---

### [Step 4: Partial Offer Request (Reprice/Lock)](04-partial-offer-request-reprice.md)
**Purpose:** Lock price and validate passenger details  
**API Call:** `POST /air/partial_offer_requests`  
**Returns:** Repriced offer with locked price  

**Key Concepts:**
- **Idempotency keys** (prevent duplicate repricing)
- Passenger data validation (names, DOB, passport)
- Price locking (5-15 minute guarantee)
- Handling price changes during repricing

---

### [Step 5: Order (Confirmed Booking)](05-order-confirmed-booking.md)
**Purpose:** Finalize booking with airline  
**API Call:** `POST /air/orders`  
**Returns:** Order ID, PNR, e-tickets  

**Key Concepts:**
- **Idempotency keys** (prevent duplicate bookings)
- Payment methods (Duffel Payments vs. balance)
- Order confirmation
- Database storage (what to store, what to purge)
- Email confirmation

---

### [Step 6: Order Management (Changes & Cancellations)](06-order-management-changes-cancellations.md)
**Purpose:** Manage bookings post-creation  
**API Calls:**  
- `GET /air/orders/{order_id}` (retrieve)  
- `POST /air/order_cancellations` (cancel)  
- `POST /air/order_change_requests` (modify)  
- Webhook endpoint (real-time updates)  

**Key Concepts:**
- Order retrieval (magic-link access)
- Cancellation flow (two-step process)
- Refund calculation
- Webhook signature verification
- Schedule change notifications

---

## ?? Quick Start Guide

### 1. Prerequisites
- **Duffel Account:** Register at https://duffel.com
- **API Keys:** Test (`duffel_test_...`) and Live (`duffel_live_...`)
- **Webhook Secret:** For signature verification
- **PostgreSQL Database:** For storing bookings

### 2. Environment Setup

```bash
# User Secrets (Development)
dotnet user-secrets set "Duffel:ApiKey" "duffel_test_xxxxx"
dotnet user-secrets set "Duffel:WebhookSecret" "whsec_xxxxx"

# AWS Secrets Manager (Production)
{
  "Duffel": {
    "ApiKey": "duffel_live_xxxxx",
    "WebhookSecret": "whsec_xxxxx"
  }
}
```

### 3. Implementation Order

**Phase 1: MVP (Core Booking Flow)**
1. ? Implement `IFlightsInfo` ? `DuffelApiClient`
2. ? Step 1: Search (offer requests)
3. ? Step 2: Display offers (DTOs)
4. ? Step 3: Retrieve offer
5. ? Step 4: Reprice (with idempotency)
6. ? Step 5: Create order (with idempotency)
7. ? Step 6: Retrieve order + webhooks

**Phase 2: Post-MVP**
- ?? Order changes (date modifications)
- ?? Seat selection
- ?? Ancillary services (extra baggage, meals)

---

## ?? Security Checklist

### API Keys
- ? Store in **User Secrets** (development)
- ? Store in **AWS Secrets Manager** (production)
- ? **Never commit to Git** (.gitignore included)
- ? Rotate keys every 90 days

### Idempotency
- ? Use UUIDs for all repricing and order creation
- ? Store keys in database with booking attempts
- ? Reuse same key for retries (network failures)
- ? Expire keys after 24 hours

### PII Handling
- ? **Store:** Order ID, PNR, first/last name, email
- ?? **Transient:** DOB, passport (encrypted, auto-purge 7-30 days)
- ? **Never Store:** Card data, CVV, raw API responses
- ? **Logging:** IDs only, never payloads with PII

### Webhooks
- ? **Verify signatures** using HMAC-SHA256
- ? Reject if timestamp > 5 minutes old (replay attack prevention)
- ? Use constant-time comparison for signatures
- ? Respond with 2xx within 5 seconds

---

## ?? Data Flow Diagram

```
???????????????????????????????????????????????????????????????????
?                   Tripppi Booking Flow                          ?
???????????????????????????????????????????????????????????????????

User Input (Origin, Destination, Dates)
         ?
[Step 1: Search]  POST /air/offer_requests
         ?
[Step 2: Display Offers]  (Frontend renders results)
         ?
User Selects Flight
         ?
[Step 3: Retrieve Offer]  GET /air/offers/{offer_id}
         ?
User Enters Passenger Details
         ?
[Step 4: Reprice]  POST /air/partial_offer_requests
         ?                      (Idempotency Key)
Price Locked (5-15 min)
         ?
User Confirms & Pays
         ?
[Step 5: Create Order]  POST /air/orders
         ?                      (Idempotency Key)
Order Confirmed (PNR Issued)
         ?
[Email Confirmation] + [Store in DB]
         ?
[Webhooks] ? Real-time updates (schedule changes, etc.)
```

---

## ?? Testing

### Sandbox Environment

Duffel provides a **test mode** for development:

**Test API Key:**
```
duffel_test_xxxxxxxxxxxxx
```

**Test Features:**
- Fake bookings (no real airline interaction)
- Test credit cards (e.g., `4242 4242 4242 4242`)
- Predictable responses
- No actual charges

### Integration Test Example

```csharp
[Fact]
public async Task FullBookingFlow_E2E_Test()
{
    // Step 1: Search
    var offerRequest = new { /* search params */ };
    var offers = await _duffelClient.SearchOffersAsync(offerRequest);
    Assert.NotEmpty(offers);
    
    // Step 2: Select offer
    var selectedOffer = offers.First();
    
    // Step 3: Retrieve offer
    var offer = await _duffelClient.GetOfferAsync(selectedOffer.Id);
    Assert.NotNull(offer);
    
    // Step 4: Reprice
    var repriceRequest = new { /* passenger details */ };
    var idempotencyKey = Guid.NewGuid().ToString();
    var repriced = await _duffelClient.RepriceOfferAsync(
        offer.Id,
        repriceRequest,
        idempotencyKey);
    Assert.Equal(offer.TotalAmount, repriced.TotalAmount);
    
    // Step 5: Create order
    var orderIdempotencyKey = Guid.NewGuid().ToString();
    var order = await _duffelClient.CreateOrderAsync(
        repriced.Id,
        repriceRequest,
        orderIdempotencyKey);
    Assert.NotNull(order.BookingReference);
}
```

---

## ?? Additional Resources

### External Documentation
- **Duffel API Docs:** https://duffel.com/docs/api/overview
- **Duffel.js SDK:** https://duffel.com/docs/guides/duffel-js (for frontend)
- **Webhook Guide:** https://duffel.com/docs/api/overview/webhooks

### Internal Documentation
- **Architecture:** [tripppi_architecture.md](../../tripppi_architecture.md)
- **IFlightsInfo Interface:** [IFlightsInfo.cs](../../appService/IFlightsInfo.cs)
- **Copilot Instructions:** [.github/copilot-instructions.md](../../../.github/copilot-instructions.md)

### Support
- **Duffel Support:** support@duffel.com
- **Duffel Slack:** Request access via dashboard

---

## ? Implementation Checklist

### Phase 1: Core Booking (MVP)
- [ ] Register Duffel account
- [ ] Obtain test API keys
- [ ] Implement `DuffelApiClient : IFlightsInfo`
- [ ] Add HTTP client configuration
- [ ] Implement Step 1 (Search)
- [ ] Implement Step 2 (Display offers with DTOs)
- [ ] Implement Step 3 (Retrieve offer)
- [ ] Implement Step 4 (Reprice with idempotency)
- [ ] Implement Step 5 (Create order with idempotency)
- [ ] Implement Step 6 (Retrieve order)
- [ ] Implement webhook handler
- [ ] Implement webhook signature verification
- [ ] Create database schema (bookings table)
- [ ] Implement email confirmation
- [ ] Test end-to-end flow in sandbox
- [ ] Obtain live API keys
- [ ] Deploy to staging
- [ ] Test with real bookings (small amounts)
- [ ] Deploy to production

### Phase 2: Post-Booking Management
- [ ] Implement order cancellations
- [ ] Implement refund flow
- [ ] Handle schedule change webhooks
- [ ] Magic-link booking retrieval

### Phase 3: Advanced Features (Post-MVP)
- [ ] Order changes (date modifications)
- [ ] Seat selection
- [ ] Ancillary services (baggage, meals)
- [ ] Multi-passenger bookings
- [ ] Corporate booking codes

---

## ?? Common Pitfalls

### 1. Missing Idempotency Keys
**Problem:** Network retries create duplicate bookings  
**Solution:** Always use idempotency keys for repricing and orders

### 2. Not Checking Price Changes
**Problem:** User sees $350, gets charged $365  
**Solution:** Compare prices after repricing, require user confirmation

### 3. Logging PII
**Problem:** Passenger names/DOB in application logs  
**Solution:** Log IDs only, never full payloads

### 4. Ignoring Offer Expiration
**Problem:** Booking fails because offer expired  
**Solution:** Check `expires_at` before every step

### 5. Not Verifying Webhook Signatures
**Problem:** Attackers can spoof webhooks  
**Solution:** Always verify HMAC-SHA256 signatures

---

## ?? Support

For questions about this integration:
1. Check relevant step documentation (links above)
2. Review [tripppi_architecture.md](../../tripppi_architecture.md)
3. Consult [IFlightsInfo.cs](../../appService/IFlightsInfo.cs) for interface contract
4. Contact Duffel support for API-specific issues

---

**Last Updated:** January 2024  
**Maintainer:** Tripppi Engineering Team  
**Version:** 1.0.0
