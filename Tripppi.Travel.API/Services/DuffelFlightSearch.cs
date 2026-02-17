using System.Text.Json;
using System.Text.Json.Serialization;
using Tripppi.Travel.API.appService;
using Tripppi.Travel.API.Services.DTOs;

namespace Tripppi.Travel.API.Services
{
    /// <summary>
    /// Duffel-specific implementation of IFlightSearch.
    /// Maps vendor-neutral DTOs to/from Duffel's API format (slices, segments, etc.).
    /// </summary>
    public sealed class DuffelFlightSearch : IFlightSearch
    {
        private readonly IFlightsInfo _duffelClient;
        private readonly ILogger<DuffelFlightSearch> _logger;

        public DuffelFlightSearch(
            IFlightsInfo duffelClient, 
            ILogger<DuffelFlightSearch> logger)
        {
            _duffelClient = duffelClient ?? throw new ArgumentNullException(nameof(duffelClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<FlightSearchResult>> SearchFlightsAsync(
            FlightSearchRequest request, 
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            _logger.LogInformation(
                "Searching flights via Duffel: {Origin} → {Destination}, {LegCount} leg(s), {PassengerCount} passenger(s)",
                request.Legs[0].OriginIataCode,
                request.Legs[0].DestinationIataCode,
                request.Legs.Count,
                request.Passengers.Count);

            try
            {
                // Map vendor-neutral request to Duffel format
                var duffelRequest = MapToDuffelRequest(request);

                // Call Duffel API via IFlightsInfo abstraction
                var responseJson = await _duffelClient.SearchOffersAsync(duffelRequest, cancellationToken);

                // Parse Duffel response
                var duffelResponse = JsonSerializer.Deserialize<DuffelOfferResponse>(
                    responseJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (duffelResponse?.Data?.Offers == null || duffelResponse.Data.Offers.Count == 0)
                {
                    _logger.LogInformation("No offers returned from Duffel");
                    return Array.Empty<FlightSearchResult>();
                }

                // Map Duffel offers to vendor-neutral results
                var results = MapToFlightSearchResults(duffelResponse);

                _logger.LogInformation("Found {Count} flight offers from Duffel", results.Count);

                return results;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Duffel API request failed with status {StatusCode}", ex.StatusCode);

                throw new FlightSearchException(
                    "Failed to search flights via Duffel",
                    ex,
                    errorCode: "duffel_api_error",
                    statusCode: (int?)ex.StatusCode,
                    isRetryable: ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable 
                               || ex.StatusCode == System.Net.HttpStatusCode.GatewayTimeout);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Duffel API response");

                throw new FlightSearchException(
                    "Invalid response format from flight provider",
                    ex,
                    errorCode: "invalid_response",
                    isRetryable: false);
            }
        }

        /// <summary>
        /// Maps vendor-neutral FlightSearchRequest to Duffel's offer request format.
        /// </summary>
        private object MapToDuffelRequest(FlightSearchRequest request)
        {
            return new
            {
                data = new
                {
                    cabin_class = MapCabinClass(request.CabinClass),
                    passengers = request.Passengers.Select(p => MapPassengerForDuffel(p)).ToArray(),
                    // Map our "legs" to Duffel's "slices"
                    slices = request.Legs.Select(leg => new
                    {
                        origin = leg.OriginIataCode,
                        destination = leg.DestinationIataCode,
                        departure_date = leg.DepartureDate.ToString("yyyy-MM-dd")
                    }).ToArray(),
                    max_connections = request.MaxConnections,
                    return_offers = request.ReturnAllOffers,
                    // Optional: exclude airlines
                    suppliers = request.ExcludedAirlines?.Count > 0
                        ? new { exclude = request.ExcludedAirlines.ToArray() }
                        : null,
                    // Optional: corporate codes
                    private_fares = request.CorporateCodes?.Count > 0
                        ? new { corporate_codes = request.CorporateCodes.ToArray() }
                        : null
                }
            };
        }

        /// <summary>
        /// Maps a passenger to Duffel format, using either type or age (mutually exclusive).
        /// Duffel requires one or the other, not both.
        /// </summary>
        private static object MapPassengerForDuffel(PassengerRequest passenger)
        {
            // If age is provided, use age-based format
            if (passenger.Age.HasValue)
            {
                return new { age = passenger.Age.Value };
            }
            
            // Otherwise, use type-based format
            return new { type = MapPassengerType(passenger.Type) };
        }

        /// <summary>
        /// Maps Duffel offer response to vendor-neutral FlightSearchResult list.
        /// </summary>
        private IReadOnlyList<FlightSearchResult> MapToFlightSearchResults(DuffelOfferResponse response)
        {
            return response.Data.Offers.Select(offer => new FlightSearchResult
            {
                OfferId = offer.Id,
                TotalAmount = ParseDecimal(offer.TotalAmount),
                Currency = offer.TotalCurrency,
                BaseAmount = ParseDecimal(offer.BaseAmount),
                TaxAmount = ParseDecimal(offer.TaxAmount),
                ExpiresAt = ParseDateTime(offer.ExpiresAt),
                RequiresInstantPayment = offer.PaymentRequirements?.RequiresInstantPayment ?? false,
                Legs = MapLegs(offer.Slices),
                Passengers = MapPassengers(offer),
                Conditions = MapConditions(offer.Conditions),
                Owner = new AirlineInfo
                {
                    IataCode = offer.Owner.IataCode,
                    Name = offer.Owner.Name,
                    LogoUrl = offer.Owner.LogoSymbolUrl
                }
            }).ToList();
        }

        /// <summary>
        /// Maps Duffel slices to vendor-neutral legs.
        /// </summary>
        private List<FlightLegResult> MapLegs(List<DuffelSlice> slices)
        {
            return slices.Select(slice => new FlightLegResult
            {
                OriginIataCode = slice.Origin.IataCode,
                OriginAirportName = slice.Origin.Name ?? string.Empty,
                OriginCityName = slice.Origin.CityName ?? string.Empty,
                DestinationIataCode = slice.Destination.IataCode,
                DestinationAirportName = slice.Destination.Name ?? string.Empty,
                DestinationCityName = slice.Destination.CityName ?? string.Empty,
                Duration = ParseDuration(slice.Duration),
                Segments = MapSegments(slice.Segments)
            }).ToList();
        }

        /// <summary>
        /// Maps Duffel segments to vendor-neutral segments.
        /// Handles null carriers, aircraft, and datetime values gracefully.
        /// </summary>
        private List<FlightSegmentResult> MapSegments(List<DuffelSegment> segments)
        {
            return segments.Select(segment => new FlightSegmentResult
            {
                OperatingCarrierIataCode = segment.OperatingCarrier?.IataCode ?? string.Empty,
                OperatingCarrierName = segment.OperatingCarrier?.Name ?? string.Empty,
                MarketingCarrierIataCode = segment.MarketingCarrier?.IataCode ?? string.Empty,
                FlightNumber = segment.MarketingCarrierFlightNumber ?? string.Empty,
                AircraftName = segment.Aircraft?.Name,
                OriginIataCode = segment.Origin?.IataCode ?? string.Empty,
                OriginTerminal = segment.OriginTerminal ?? segment.Origin?.Terminal,
                DestinationIataCode = segment.Destination?.IataCode ?? string.Empty,
                DestinationTerminal = segment.DestinationTerminal ?? segment.Destination?.Terminal,
                DepartingAt = ParseDateTime(segment.DepartingAt),
                ArrivingAt = ParseDateTime(segment.ArrivingAt),
                Duration = ParseDuration(segment.Duration)
            }).ToList();
        }

        /// <summary>
        /// Maps Duffel passengers to vendor-neutral passengers.
        /// Note: Duffel stores detailed passenger info (baggage, cabin class marketing name) in segments, not at offer level.
        /// We extract from the first segment's passengers array.
        /// </summary>
        private List<PassengerResult> MapPassengers(DuffelOffer offer)
        {
            // Get passenger details from segments (they have baggage, cabin class marketing name, etc.)
            var segmentPassengers = offer.Slices
                .SelectMany(s => s.Segments)
                .SelectMany(seg => seg.Passengers ?? new List<DuffelSegmentPassenger>())
                .ToList();

            return offer.Passengers.Select(p =>
            {
                // Find matching segment passenger by ID for detailed info
                var segmentPassenger = segmentPassengers.FirstOrDefault(sp => sp.PassengerId == p.Id);

                return new PassengerResult
                {
                    Type = ParsePassengerType(p.Type),
                    CabinClass = ParseCabinClass(segmentPassenger?.CabinClass),
                    CabinClassMarketingName = segmentPassenger?.CabinClassMarketingName,
                    FareBasisCode = segmentPassenger?.FareBasisCode,
                    Baggages = MapBaggages(segmentPassenger?.Baggages)
                };
            }).ToList();
        }

        /// <summary>
        /// Maps Duffel baggage allowances.
        /// </summary>
        private List<BaggageAllowance> MapBaggages(List<DuffelBaggage>? baggages)
        {
            if (baggages == null || baggages.Count == 0)
                return new List<BaggageAllowance>();

            return baggages.Select(b => new BaggageAllowance
            {
                Type = b.Type.ToLowerInvariant() switch
                {
                    "checked" => BaggageType.Checked,
                    "carry_on" => BaggageType.CarryOn,
                    _ => BaggageType.CarryOn
                },
                Quantity = b.Quantity
            }).ToList();
        }

        /// <summary>
        /// Maps Duffel fare conditions to vendor-neutral format.
        /// </summary>
        private FareConditions MapConditions(DuffelConditions? conditions)
        {
            if (conditions == null)
            {
                return new FareConditions
                {
                    ChangeBeforeDepartureAllowed = false,
                    RefundBeforeDepartureAllowed = false,
                    ChangeAfterDepartureAllowed = false,
                    RefundAfterDepartureAllowed = false
                };
            }

            return new FareConditions
            {
                ChangeBeforeDepartureAllowed = conditions.ChangeBeforeDeparture?.Allowed ?? false,
                ChangeBeforeDeparturePenaltyAmount = ParseDecimalNullable(conditions.ChangeBeforeDeparture?.PenaltyAmount),
                ChangeBeforeDeparturePenaltyCurrency = conditions.ChangeBeforeDeparture?.PenaltyCurrency,
                RefundBeforeDepartureAllowed = conditions.RefundBeforeDeparture?.Allowed ?? false,
                ChangeAfterDepartureAllowed = conditions.ChangeAfterDeparture?.Allowed ?? false,
                RefundAfterDepartureAllowed = conditions.RefundAfterDeparture?.Allowed ?? false
            };
        }

        #region Helper Methods

        /// <summary>
        /// Safely parses a decimal string, returning 0 if null or invalid.
        /// </summary>
        private static decimal ParseDecimal(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0m;
            
            return decimal.TryParse(value, out var result) ? result : 0m;
        }

        /// <summary>
        /// Safely parses a nullable decimal string, returning null if empty or invalid.
        /// </summary>
        private static decimal? ParseDecimalNullable(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            
            return decimal.TryParse(value, out var result) ? result : null;
        }

        /// <summary>
        /// Safely parses a DateTime string, returning DateTime.MinValue if null or invalid.
        /// </summary>
        private static DateTime ParseDateTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DateTime.MinValue;
            
            return DateTime.TryParse(value, out var result) ? result : DateTime.MinValue;
        }

        private static string MapCabinClass(CabinClass cabinClass) => cabinClass switch
        {
            CabinClass.Economy => "economy",
            CabinClass.PremiumEconomy => "premium_economy",
            CabinClass.Business => "business",
            CabinClass.First => "first",
            _ => "economy"
        };

        private static string MapPassengerType(PassengerType type) => type switch
        {
            PassengerType.Adult => "adult",
            PassengerType.Child => "child",
            PassengerType.InfantWithoutSeat => "infant_without_seat",
            _ => "adult"
        };

        private static CabinClass ParseCabinClass(string? cabinClass) => cabinClass?.ToLowerInvariant() switch
        {
            "economy" => CabinClass.Economy,
            "premium_economy" => CabinClass.PremiumEconomy,
            "business" => CabinClass.Business,
            "first" => CabinClass.First,
            _ => CabinClass.Economy
        };

        private static PassengerType ParsePassengerType(string type) => type.ToLowerInvariant() switch
        {
            "adult" => PassengerType.Adult,
            "child" => PassengerType.Child,
            "infant_without_seat" => PassengerType.InfantWithoutSeat,
            _ => PassengerType.Adult
        };

        /// <summary>
        /// Parses ISO 8601 duration string (e.g., "PT2H30M") to TimeSpan.
        /// </summary>
        private static TimeSpan ParseDuration(string duration)
        {
            if (string.IsNullOrWhiteSpace(duration))
                return TimeSpan.Zero;
            
            try
            {
                return System.Xml.XmlConvert.ToTimeSpan(duration);
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        #endregion

        #region Duffel Response Models (Internal)

        // These are internal models for deserializing Duffel's JSON response
        // They map directly to Duffel's API structure

        private sealed class DuffelOfferResponse
        {
            [JsonPropertyName("data")]
            public DuffelOfferData Data { get; set; } = null!;
        }

        private sealed class DuffelOfferData
        {
            // Offer request ID (e.g., "orq_xxx") - optional as it may not be in all responses
            [JsonPropertyName("id")]
            public string? Id { get; set; }
            
            // List of offers returned from search
            [JsonPropertyName("offers")]
            public List<DuffelOffer> Offers { get; set; } = new();
        }

        private sealed class DuffelOffer
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = null!;
            
            [JsonPropertyName("total_amount")]
            public string TotalAmount { get; set; } = null!;
            
            [JsonPropertyName("total_currency")]
            public string TotalCurrency { get; set; } = null!;
            
            [JsonPropertyName("base_amount")]
            public string? BaseAmount { get; set; }
            
            [JsonPropertyName("tax_amount")]
            public string? TaxAmount { get; set; }
            
            [JsonPropertyName("expires_at")]
            public string ExpiresAt { get; set; } = null!;
            
            [JsonPropertyName("payment_requirements")]
            public DuffelPaymentRequirements? PaymentRequirements { get; set; }
            
            [JsonPropertyName("slices")]
            public List<DuffelSlice> Slices { get; set; } = new();
            
            [JsonPropertyName("passengers")]
            public List<DuffelPassenger> Passengers { get; set; } = new();
            
            [JsonPropertyName("conditions")]
            public DuffelConditions? Conditions { get; set; }
            
            [JsonPropertyName("owner")]
            public DuffelOwner Owner { get; set; } = null!;
        }

        private sealed class DuffelPaymentRequirements
        {
            [JsonPropertyName("requires_instant_payment")]
            public bool RequiresInstantPayment { get; set; }
        }

        private sealed class DuffelSlice
        {
            [JsonPropertyName("origin")]
            public DuffelPlace Origin { get; set; } = null!;
            
            [JsonPropertyName("destination")]
            public DuffelPlace Destination { get; set; } = null!;
            
            [JsonPropertyName("duration")]
            public string Duration { get; set; } = null!;
            
            [JsonPropertyName("segments")]
            public List<DuffelSegment> Segments { get; set; } = new();
        }

        private sealed class DuffelSegment
        {
            [JsonPropertyName("operating_carrier")]
            public DuffelCarrier? OperatingCarrier { get; set; }
            
            [JsonPropertyName("marketing_carrier")]
            public DuffelCarrier? MarketingCarrier { get; set; }
            
            [JsonPropertyName("marketing_carrier_flight_number")]
            public string? MarketingCarrierFlightNumber { get; set; }
            
            [JsonPropertyName("aircraft")]
            public DuffelAircraft? Aircraft { get; set; }
            
            [JsonPropertyName("origin")]
            public DuffelPlace? Origin { get; set; }
            
            [JsonPropertyName("origin_terminal")]
            public string? OriginTerminal { get; set; }
            
            [JsonPropertyName("destination")]
            public DuffelPlace? Destination { get; set; }
            
            [JsonPropertyName("destination_terminal")]
            public string? DestinationTerminal { get; set; }
            
            [JsonPropertyName("departing_at")]
            public string? DepartingAt { get; set; }
            
            [JsonPropertyName("arriving_at")]
            public string? ArrivingAt { get; set; }
            
            [JsonPropertyName("duration")]
            public string Duration { get; set; } = null!;
            
            [JsonPropertyName("passengers")]
            public List<DuffelSegmentPassenger>? Passengers { get; set; }
        }

        private sealed class DuffelPlace
        {
            [JsonPropertyName("iata_code")]
            public string IataCode { get; set; } = null!;
            
            [JsonPropertyName("name")]
            public string? Name { get; set; }
            
            [JsonPropertyName("city_name")]
            public string? CityName { get; set; }
            
            [JsonPropertyName("terminal")]
            public string? Terminal { get; set; }
        }

        private sealed class DuffelCarrier
        {
            [JsonPropertyName("iata_code")]
            public string IataCode { get; set; } = null!;
            
            [JsonPropertyName("name")]
            public string Name { get; set; } = null!;
        }

        private sealed class DuffelAircraft
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        private sealed class DuffelPassenger
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = null!;
            
            [JsonPropertyName("type")]
            public string Type { get; set; } = null!;
            
            [JsonPropertyName("cabin_class")]
            public string? CabinClass { get; set; }
            
            [JsonPropertyName("cabin_class_marketing_name")]
            public string? CabinClassMarketingName { get; set; }
            
            [JsonPropertyName("fare_basis_code")]
            public string? FareBasisCode { get; set; }
            
            [JsonPropertyName("baggages")]
            public List<DuffelBaggage>? Baggages { get; set; }
        }

        private sealed class DuffelBaggage
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = null!;
            
            [JsonPropertyName("quantity")]
            public int Quantity { get; set; }
        }

        private sealed class DuffelConditions
        {
            [JsonPropertyName("change_before_departure")]
            public DuffelChangeRefundCondition? ChangeBeforeDeparture { get; set; }
            
            [JsonPropertyName("refund_before_departure")]
            public DuffelChangeRefundCondition? RefundBeforeDeparture { get; set; }
            
            [JsonPropertyName("change_after_departure")]
            public DuffelChangeRefundCondition? ChangeAfterDeparture { get; set; }
            
            [JsonPropertyName("refund_after_departure")]
            public DuffelChangeRefundCondition? RefundAfterDeparture { get; set; }
        }

        private sealed class DuffelChangeRefundCondition
        {
            [JsonPropertyName("allowed")]
            public bool Allowed { get; set; }
            
            [JsonPropertyName("penalty_amount")]
            public string? PenaltyAmount { get; set; }
            
            [JsonPropertyName("penalty_currency")]
            public string? PenaltyCurrency { get; set; }
        }

        private sealed class DuffelOwner
        {
            [JsonPropertyName("iata_code")]
            public string IataCode { get; set; } = null!;
            
            [JsonPropertyName("name")]
            public string Name { get; set; } = null!;
            
            [JsonPropertyName("logo_symbol_url")]
            public string? LogoSymbolUrl { get; set; }
        }

        private sealed class DuffelSegmentPassenger
        {
            [JsonPropertyName("passenger_id")]
            public string PassengerId { get; set; } = null!;
            
            [JsonPropertyName("cabin_class")]
            public string? CabinClass { get; set; }
            
            [JsonPropertyName("cabin_class_marketing_name")]
            public string? CabinClassMarketingName { get; set; }
            
            [JsonPropertyName("fare_basis_code")]
            public string? FareBasisCode { get; set; }
            
            [JsonPropertyName("baggages")]
            public List<DuffelBaggage>? Baggages { get; set; }
        }

        #endregion
    }
}
