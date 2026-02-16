# Step 4: Partial Offer Request (Reprice/Lock)

## Overview
A **Partial Offer Request** (also called "repricing") validates the offer's availability and locks in the price before creating the final order. This critical step prevents price changes during checkout and validates passenger details with the airline.

---

## Purpose
- **Lock in the price** temporarily (typically 5-15 minutes)
- **Validate passenger details** (names, DOB, passport) with airline systems
- **Get final pricing** including any passenger-specific fees
- **Prevent double-booking** via idempotency keys
- **Required by some airlines** for accurate pricing (e.g., infant fares)

---

## API Endpoint

```http
POST https://api.duffel.com/air/partial_offer_requests
Content-Type: application/json
Authorization: Bearer duffel_live_xxxxxxxxxxxxx
Idempotency-Key: {unique_uuid}
```

---

## Next Steps

After successful repricing:
1. **Store repriced offer ID** (different from original!)
2. **Display final price** to user
3. **Show payment form**
4. Proceed to **Step 5: Create Order**

---

## References

- **Duffel API Docs**: https://duffel.com/docs/api/overview/partial-offer-requests
- **Previous Step**: [03-offer-single-selection.md](03-offer-single-selection.md)
- **Next Step**: [05-order-confirmed-booking.md](05-order-confirmed-booking.md)
- **IFlightsInfo Interface**: [IFlightsInfo.cs](../../appService/IFlightsInfo.cs)
