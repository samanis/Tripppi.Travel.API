# Quick Start Guide: Testing the Flights API

## Prerequisites
- .NET 10 SDK installed
- Duffel API keys configured (for real searches)

---

## 1. Start the API

```bash
cd Tripppi.Travel.API
dotnet run
```

**Output:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5063
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

---

## 2. Test Health Endpoint

### Using cURL:
```bash
curl http://localhost:5063/api/flights/health
```

### Expected Response:
```json
{
  "service": "FlightSearch",
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "provider": "Duffel"
}
```

---

## 3. Open Swagger UI

Navigate to: **http://localhost:5063/swagger**

You should see:
- ? `POST /api/flights/search` - Search for flights
- ? `GET /api/flights/health` - Health check
- ? All existing endpoints (airports, etc.)

---

## 4. Test Flight Search (Swagger UI)

1. **Expand** `POST /api/flights/search`
2. **Click** "Try it out"
3. **Paste** this request:

```json
{
  "legs": [
    {
      "originIataCode": "YYZ",
      "destinationIataCode": "LHR",
      "departureDate": "2024-12-15"
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

4. **Click** "Execute"

### Expected Behavior:

**Without Duffel API keys configured:**
```json
{
  "status": 500,
  "title": "Flight search failed",
  "detail": "An error occurred while searching for flights. Please try again."
}
```
*(This is expected - IFlightsInfo needs to be configured with Duffel credentials)*

**With Duffel API keys configured:**
```json
[
  {
    "offerId": "off_...",
    "totalAmount": 450.50,
    "currency": "USD",
    "legs": [...],
    "passengers": [...]
  }
]
```

---

## 5. Test with cURL

### Search Flights:
```bash
curl -X POST "http://localhost:5063/api/flights/search" \
  -H "Content-Type: application/json" \
  -d '{
    "legs": [
      {
        "originIataCode": "YYZ",
        "destinationIataCode": "LHR",
        "departureDate": "2024-12-15"
      }
    ],
    "passengers": [{"type": "Adult", "age": 30}],
    "cabinClass": "Economy"
  }'
```

### Invalid Request (should return 400):
```bash
curl -X POST "http://localhost:5063/api/flights/search" \
  -H "Content-Type: application/json" \
  -d '{
    "legs": [],
    "passengers": [],
    "cabinClass": "Economy"
  }'
```

---

## 6. Check Logs

Look for these log entries:

```
[Information] Flight search request: YYZ ? LHR, 1 leg(s), 1 passenger(s), Cabin: Economy
[Information] Searching flights via Duffel: YYZ ? LHR, 1 leg(s), 1 passenger(s)
```

**Verify:**
- ? No PII in logs (no passenger names, DOB, etc.)
- ? Only search parameters logged
- ? Structured logging format

---

## 7. Configure Duffel API (Optional)

To make real searches work, configure `IFlightsInfo` implementation:

**Option 1: appsettings.Development.json**
```json
{
  "Duffel": {
    "ApiKey": "duffel_test_...",
    "BaseUrl": "https://api.duffel.com"
  }
}
```

**Option 2: Environment Variables**
```bash
export DUFFEL__APIKEY="duffel_test_..."
export DUFFEL__BASEURL="https://api.duffel.com"
```

**Option 3: User Secrets**
```bash
dotnet user-secrets set "Duffel:ApiKey" "duffel_test_..."
```

---

## 8. Run Unit Tests

```bash
cd Tripppi.Travel.Api.Test
dotnet test --filter "DuffelFlightSearchTests"
```

**Expected:**
```
Test run for Tripppi.Travel.Api.Test.dll (.NET 10.0)
Total tests: 10
     Passed: 10
    Skipped: 0
     Failed: 0
```

---

## 9. Common Issues

### Issue: "Service temporarily unavailable (503)"
**Cause:** IFlightsInfo not configured or Duffel API down  
**Solution:** Configure Duffel API keys or mock IFlightsInfo for testing

### Issue: "Rate limit exceeded (429)"
**Cause:** Too many requests to Duffel API  
**Solution:** Wait 60 seconds or use different API keys

### Issue: "Invalid search parameters (400)"
**Cause:** Missing required fields or invalid IATA codes  
**Solution:** Check request validation errors in response

---

## 10. Next Steps

1. **Configure Duffel API keys** - Get test keys from Duffel dashboard
2. **Test real searches** - Try YYZ?LHR, JFK?LAX, etc.
3. **Monitor logs** - Watch for errors or performance issues
4. **Deploy to staging** - Configure environment variables
5. **Add more features** - Repricing, offer details, filtering

---

## ? Success Checklist

- [ ] API starts successfully
- [ ] Health endpoint returns 200
- [ ] Swagger UI loads
- [ ] Search endpoint accepts requests
- [ ] Validation works (400 for invalid requests)
- [ ] Logs show no PII
- [ ] Unit tests pass (10/10)
- [ ] Ready for Duffel API configuration

---

## ?? Documentation

- **Full Implementation:** `Services/README-DuffelFlightSearch.md`
- **Deployment Summary:** `docs/DuffelFlightSearch-Deployment-Summary.md`
- **Architecture:** `.github/copilot-instructions.md`

**Status:** ? Deployed and Ready
