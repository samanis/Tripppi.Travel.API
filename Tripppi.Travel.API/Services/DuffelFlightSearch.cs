using System.Text.Json;
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
                "Searching flights via Duffel: {Origin} ? {Destination}, {LegCount} leg(s), {PassengerCount} passenger(s)",
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
                    passengers = request.Passengers.Select(p => new
                    {
                        type = MapPassengerType(p.Type),
                        age = p.Age
                    }).ToArray(),
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
        /// Maps Duffel offer response to vendor-neutral FlightSearchResult list.
        /// </summary>
        private IReadOnlyList<FlightSearchResult> MapToFlightSearchResults(DuffelOfferResponse response)
        {
            return response.Data.Offers.Select(offer => new FlightSearchResult
            {
                OfferId = offer.Id,
                TotalAmount = decimal.Parse(offer.TotalAmount),
                Currency = offer.TotalCurrency,
                BaseAmount = decimal.Parse(offer.BaseAmount ?? "0"),
                TaxAmount = decimal.Parse(offer.TaxAmount ?? "0"),
                ExpiresAt = DateTime.Parse(offer.ExpiresAt),
                RequiresInstantPayment = offer.PaymentRequirements?.RequiresInstantPayment ?? false,
                Legs = MapLegs(offer.Slices),
                Passengers = MapPassengers(offer.Passengers),
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
        /// </summary>
        private List<FlightSegmentResult> MapSegments(List<DuffelSegment> segments)
        {
            return segments.Select(segment => new FlightSegmentResult
            {
                OperatingCarrierIataCode = segment.OperatingCarrier.IataCode,
                OperatingCarrierName = segment.OperatingCarrier.Name,
                MarketingCarrierIataCode = segment.MarketingCarrier.IataCode,
                FlightNumber = segment.MarketingCarrierFlightNumber,
                AircraftName = segment.Aircraft?.Name,
                OriginIataCode = segment.Origin.IataCode,
                OriginTerminal = segment.Origin.Terminal,
                DestinationIataCode = segment.Destination.IataCode,
                DestinationTerminal = segment.Destination.Terminal,
                DepartingAt = DateTime.Parse(segment.DepartingAt),
                ArrivingAt = DateTime.Parse(segment.ArrivingAt),
                Duration = ParseDuration(segment.Duration)
            }).ToList();
        }

        /// <summary>
        /// Maps Duffel passengers to vendor-neutral passengers.
        /// </summary>
        private List<PassengerResult> MapPassengers(List<DuffelPassenger> passengers)
        {
            return passengers.Select(p => new PassengerResult
            {
                Type = ParsePassengerType(p.Type),
                CabinClass = ParseCabinClass(p.CabinClass),
                CabinClassMarketingName = p.CabinClassMarketingName,
                FareBasisCode = p.FareBasisCode,
                Baggages = MapBaggages(p.Baggages)
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
                ChangeBeforeDeparturePenaltyAmount = conditions.ChangeBeforeDeparture?.PenaltyAmount != null
                    ? decimal.Parse(conditions.ChangeBeforeDeparture.PenaltyAmount)
                    : null,
                ChangeBeforeDeparturePenaltyCurrency = conditions.ChangeBeforeDeparture?.PenaltyCurrency,
                RefundBeforeDepartureAllowed = conditions.RefundBeforeDeparture?.Allowed ?? false,
                ChangeAfterDepartureAllowed = conditions.ChangeAfterDeparture?.Allowed ?? false,
                RefundAfterDepartureAllowed = conditions.RefundAfterDeparture?.Allowed ?? false
            };
        }

        #region Helper Methods

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
            public DuffelOfferData Data { get; set; } = null!;
        }

        private sealed class DuffelOfferData
        {
            public string Id { get; set; } = null!;
            public List<DuffelOffer> Offers { get; set; } = new();
        }

        private sealed class DuffelOffer
        {
            public string Id { get; set; } = null!;
            public string TotalAmount { get; set; } = null!;
            public string TotalCurrency { get; set; } = null!;
            public string? BaseAmount { get; set; }
            public string? TaxAmount { get; set; }
            public string ExpiresAt { get; set; } = null!;
            public DuffelPaymentRequirements? PaymentRequirements { get; set; }
            public List<DuffelSlice> Slices { get; set; } = new();
            public List<DuffelPassenger> Passengers { get; set; } = new();
            public DuffelConditions? Conditions { get; set; }
            public DuffelOwner Owner { get; set; } = null!;
        }

        private sealed class DuffelPaymentRequirements
        {
            public bool RequiresInstantPayment { get; set; }
        }

        private sealed class DuffelSlice
        {
            public DuffelPlace Origin { get; set; } = null!;
            public DuffelPlace Destination { get; set; } = null!;
            public string Duration { get; set; } = null!;
            public List<DuffelSegment> Segments { get; set; } = new();
        }

        private sealed class DuffelSegment
        {
            public DuffelCarrier OperatingCarrier { get; set; } = null!;
            public DuffelCarrier MarketingCarrier { get; set; } = null!;
            public string MarketingCarrierFlightNumber { get; set; } = null!;
            public DuffelAircraft? Aircraft { get; set; }
            public DuffelPlace Origin { get; set; } = null!;
            public DuffelPlace Destination { get; set; } = null!;
            public string DepartingAt { get; set; } = null!;
            public string ArrivingAt { get; set; } = null!;
            public string Duration { get; set; } = null!;
        }

        private sealed class DuffelPlace
        {
            public string IataCode { get; set; } = null!;
            public string? Name { get; set; }
            public string? CityName { get; set; }
            public string? Terminal { get; set; }
        }

        private sealed class DuffelCarrier
        {
            public string IataCode { get; set; } = null!;
            public string Name { get; set; } = null!;
        }

        private sealed class DuffelAircraft
        {
            public string? Name { get; set; }
        }

        private sealed class DuffelPassenger
        {
            public string Type { get; set; } = null!;
            public string? CabinClass { get; set; }
            public string? CabinClassMarketingName { get; set; }
            public string? FareBasisCode { get; set; }
            public List<DuffelBaggage>? Baggages { get; set; }
        }

        private sealed class DuffelBaggage
        {
            public string Type { get; set; } = null!;
            public int Quantity { get; set; }
        }

        private sealed class DuffelConditions
        {
            public DuffelChangeRefundCondition? ChangeBeforeDeparture { get; set; }
            public DuffelChangeRefundCondition? RefundBeforeDeparture { get; set; }
            public DuffelChangeRefundCondition? ChangeAfterDeparture { get; set; }
            public DuffelChangeRefundCondition? RefundAfterDeparture { get; set; }
        }

        private sealed class DuffelChangeRefundCondition
        {
            public bool Allowed { get; set; }
            public string? PenaltyAmount { get; set; }
            public string? PenaltyCurrency { get; set; }
        }

        private sealed class DuffelOwner
        {
            public string IataCode { get; set; } = null!;
            public string Name { get; set; } = null!;
            public string? LogoSymbolUrl { get; set; }
        }

        #endregion
    }
}
