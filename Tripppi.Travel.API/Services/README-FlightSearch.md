# Flight Search Abstraction Layer

## Overview

The `IFlightSearch` interface provides a **vendor-neutral abstraction** for flight search operations in Tripppi.Travel.API. This design allows switching between different flight providers (Duffel, Amadeus, Sabre, etc.) without changing business logic or controllers.

---

## Terminology

**Flight Leg vs. Segment:**
- **Leg**: A complete journey from origin to destination (e.g., "Toronto to New York")
  - One-way trip = 1 leg
  - Round-trip = 2 legs (outbound + return)
  - A leg may contain multiple segments if there are connections
  
- **Segment**: An individual flight operated by one aircraft (e.g., "AA 1234 from YYZ to ORD")
  - Direct flight = 1 segment per leg
  - Flight with layover = 2+ segments per leg

**Example:**
```
Request: Toronto ? Paris (round-trip)

Leg 1 (Outbound): YYZ ? CDG
  ?? Segment 1: YYZ ? ORD (AA 1234)
  ?? Segment 2: ORD ? CDG (AA 5678)

Leg 2 (Return): CDG ? YYZ
  ?? Segment 1: CDG ? YYZ (AA 9012, direct)
