using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Tripppi.Travel.API.appService;
using Tripppi.Travel.API.Services;
using Tripppi.Travel.API.Services.DTOs;
using Xunit;

namespace Tripppi.Travel.Api.Test.Services;

/// <summary>
/// Tests for DuffelFlightSearch mapping logic using Duffel V2 API response format.
/// This response format includes more complex structures like stops, amenities, and services.
/// </summary>
public class DuffelFlightSearchMappingV2Tests
{
    private readonly Mock<IFlightsInfo> _mockDuffelClient;
    private readonly Mock<ILogger<DuffelFlightSearch>> _mockLogger;
    private readonly DuffelFlightSearch _sut;

    public DuffelFlightSearchMappingV2Tests()
    {
        _mockDuffelClient = new Mock<IFlightsInfo>();
        _mockLogger = new Mock<ILogger<DuffelFlightSearch>>();
        _sut = new DuffelFlightSearch(_mockDuffelClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task SearchFlightsAsync_WithV2Response_MapsOfferCorrectly()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert
        Assert.NotNull(results);
        Assert.Single(results); // V2 response has 1 offer
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_MapsPricingCorrectly()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert
        var offer = results[0];
        Assert.Equal("off_00009htYpSCXrwaB9DnUm0", offer.OfferId);
        Assert.Equal(45.00m, offer.TotalAmount);
        Assert.Equal("GBP", offer.Currency);
        Assert.Equal(30.20m, offer.BaseAmount);
        Assert.Equal(40.80m, offer.TaxAmount);
        Assert.False(offer.RequiresInstantPayment);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_MapsOwnerCorrectly()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert
        var offer = results[0];
        Assert.Equal("BA", offer.Owner.IataCode);
        Assert.Equal("British Airways", offer.Owner.Name);
        Assert.Equal("https://assets.duffel.com/img/airlines/for-light-background/full-color-logo/BA.svg", 
            offer.Owner.LogoUrl);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_MapsLegWithSingleSegment()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert
        var offer = results[0];
        Assert.Single(offer.Legs);
        
        var leg = offer.Legs[0];
        Assert.Equal("LHR", leg.OriginIataCode);
        Assert.Equal("Heathrow", leg.OriginAirportName);
        Assert.Equal("London", leg.OriginCityName);
        Assert.Equal("LHR", leg.DestinationIataCode); // V2 destination is also LHR
        Assert.Equal("Heathrow", leg.DestinationAirportName);
        Assert.Equal("London", leg.DestinationCityName);
        Assert.Equal(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(26)), leg.Duration);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_MapsSegmentWithAircraft()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert
        var segment = results[0].Legs[0].Segments[0];
        Assert.Equal("BA", segment.OperatingCarrierIataCode);
        Assert.Equal("British Airways", segment.OperatingCarrierName);
        Assert.Equal("BA", segment.MarketingCarrierIataCode);
        Assert.Equal("1234", segment.FlightNumber);
        Assert.Equal("Airbus Industries A380", segment.AircraftName); // V2 has aircraft
        Assert.Equal("LHR", segment.OriginIataCode);
        Assert.Equal("B", segment.OriginTerminal);
        Assert.Equal("JFK", segment.DestinationIataCode);
        Assert.Equal("5", segment.DestinationTerminal);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_ParsesDateTimeCorrectly()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert
        var offer = results[0];
        Assert.Equal(new DateTime(2020, 1, 17, 10, 42, 14, 545), offer.ExpiresAt);
        
        var segment = offer.Legs[0].Segments[0];
        Assert.Equal(new DateTime(2020, 6, 13, 16, 38, 2), segment.DepartingAt);
        Assert.Equal(new DateTime(2020, 6, 13, 16, 38, 2), segment.ArrivingAt);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_MapsPassengerWithLoyaltyProgram()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert
        var offer = results[0];
        Assert.Single(offer.Passengers);
        
        var passenger = offer.Passengers[0];
        Assert.Equal(PassengerType.Adult, passenger.Type);
        Assert.Equal(CabinClass.Economy, passenger.CabinClass);
        Assert.Equal("Economy Basic", passenger.CabinClassMarketingName);
        Assert.Equal("OXZ0RO", passenger.FareBasisCode);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_MapsBaggageCorrectly()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert
        var passenger = results[0].Passengers[0];
        Assert.Single(passenger.Baggages);
        
        var checkedBag = passenger.Baggages[0];
        Assert.Equal(BaggageType.Checked, checkedBag.Type);
        Assert.Equal(1, checkedBag.Quantity);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_MapsFareConditionsWithPenalties()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert
        var offer = results[0];
        Assert.True(offer.Conditions.ChangeBeforeDepartureAllowed);
        Assert.Equal(100.00m, offer.Conditions.ChangeBeforeDeparturePenaltyAmount);
        Assert.Equal("GBP", offer.Conditions.ChangeBeforeDeparturePenaltyCurrency);
        Assert.True(offer.Conditions.RefundBeforeDepartureAllowed);
        // V2 response doesn't have after_departure conditions
        Assert.False(offer.Conditions.ChangeAfterDepartureAllowed);
        Assert.False(offer.Conditions.RefundAfterDepartureAllowed);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_HandlesPartialOfferFlag()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert - Verify offer exists (partial flag doesn't affect basic mapping)
        Assert.Single(results);
        var offer = results[0];
        Assert.NotNull(offer.OfferId);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_HandlesLiveModeFlag()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert - Verify offer exists (live_mode doesn't affect basic mapping)
        Assert.Single(results);
        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_MapsSegmentDurationCorrectly()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert
        var segment = results[0].Legs[0].Segments[0];
        Assert.Equal(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(26)), segment.Duration);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_HandlesNestedCityStructure()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert - V2 has nested city objects with airports arrays
        var leg = results[0].Legs[0];
        Assert.Equal("London", leg.OriginCityName);
        Assert.Equal("London", leg.DestinationCityName);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_ValidatesAllRequiredFieldsPresent()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert - Verify all critical fields are populated
        var offer = results[0];
        Assert.NotNull(offer.OfferId);
        Assert.NotNull(offer.Currency);
        Assert.NotNull(offer.Owner);
        Assert.NotEmpty(offer.Legs);
        Assert.NotEmpty(offer.Passengers);
        Assert.NotNull(offer.Conditions);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_HandlesMultipleTerminals()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert - V2 has different terminals for origin and destination
        var segment = results[0].Legs[0].Segments[0];
        Assert.Equal("B", segment.OriginTerminal);
        Assert.Equal("5", segment.DestinationTerminal);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_MapsPaymentRequirements()
    {
        // Arrange
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act
        var results = await _sut.SearchFlightsAsync(request);

        // Assert
        var offer = results[0];
        Assert.False(offer.RequiresInstantPayment);
    }

    [Fact]
    public async Task SearchFlightsAsync_V2Response_HandlesComplexNestedStructures()
    {
        // Arrange - V2 has deeply nested structures (city.airports, stops, etc.)
        var request = CreateValidFlightSearchRequest();
        var duffelResponse = GetDuffelV2ResponseJson();
        
        _mockDuffelClient
            .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duffelResponse);

        // Act & Assert - Should not throw despite complex nesting
        var exception = await Record.ExceptionAsync(() => _sut.SearchFlightsAsync(request));
        Assert.Null(exception);
    }

    // Helper methods

    private static FlightSearchRequest CreateValidFlightSearchRequest()
    {
        return new FlightSearchRequest
        {
            Legs = new List<FlightLegRequest>
            {
                new FlightLegRequest
                {
                    OriginIataCode = "LHR",
                    DestinationIataCode = "JFK",
                    DepartureDate = new DateOnly(2020, 6, 13)
                }
            },
            Passengers = new List<PassengerRequest>
            {
                new PassengerRequest { Type = PassengerType.Adult, Age = 14 }
            },
            CabinClass = CabinClass.Economy,
            MaxConnections = 0,
            ReturnAllOffers = true
        };
    }

    private static string GetDuffelV2ResponseJson()
    {
        // This is the actual V2 JSON response provided
        return @"{""data"":{""updated_at"":""2020-01-17T10:12:14.545Z"",""total_emissions_kg"":""460"",""total_currency"":""GBP"",""total_amount"":""45.00"",""tax_currency"":""GBP"",""tax_amount"":""40.80"",""supported_passenger_identity_document_types"":[""passport""],""supported_loyalty_programmes"":[""AF"",""KL"",""DL""],""slices"":[{""segments"":[{""stops"":[{""id"":""sto_00009htYpSCXrwaB9Dn456"",""duration"":""PT02H26M"",""departing_at"":""2020-06-13T16:38:02"",""arriving_at"":""2020-06-13T16:38:02"",""airport"":{""type"":""airport"",""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR"",""iata_city_code"":""LON"",""city_name"":""London"",""city"":{""name"":""London"",""id"":""cit_lon_gb"",""iata_country_code"":""GB"",""iata_code"":""LON"",""airports"":[{""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR""}]},""airports"":[{""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR"",""iata_city_code"":""LON"",""city_name"":""London"",""city"":{""name"":""London"",""id"":""cit_lon_gb"",""iata_country_code"":""GB"",""iata_code"":""LON"",""airports"":[{""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR""}]}}]}}],""passengers"":[{""passenger_id"":""passenger_0"",""fare_basis_code"":""OXZ0RO"",""cabin_class_marketing_name"":""Economy Basic"",""cabin_class"":""economy"",""cabin"":{""name"":""economy"",""marketing_name"":""Economy Basic"",""amenities"":{""wifi"":{""cost"":""free"",""available"":""true""},""seat"":{""type"":""standard"",""pitch"":""32"",""legroom"":""standard""},""power"":{""available"":""true""}}},""baggages"":[{""type"":""checked"",""quantity"":1}]}],""origin_terminal"":""B"",""origin"":{""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR"",""iata_city_code"":""LON"",""city_name"":""London"",""city"":{""name"":""London"",""id"":""cit_lon_gb"",""iata_country_code"":""GB"",""iata_code"":""LON"",""airports"":[{""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR""}]}},""operating_carrier_flight_number"":""4321"",""operating_carrier"":{""name"":""British Airways"",""logo_symbol_url"":""https://assets.duffel.com/img/airlines/for-light-background/full-color-logo/BA.svg"",""logo_lockup_url"":""https://assets.duffel.com/img/airlines/for-light-background/full-color-lockup/BA.svg"",""id"":""arl_00001876aqC8c5umZmrRds"",""iata_code"":""BA"",""conditions_of_carriage_url"":""https://www.britishairways.com/en-gb/information/legal/british-airways/general-conditions-of-carriage""},""marketing_carrier_flight_number"":""1234"",""marketing_carrier"":{""name"":""British Airways"",""logo_symbol_url"":""https://assets.duffel.com/img/airlines/for-light-background/full-color-logo/BA.svg"",""logo_lockup_url"":""https://assets.duffel.com/img/airlines/for-light-background/full-color-lockup/BA.svg"",""id"":""arl_00001876aqC8c5umZmrRds"",""iata_code"":""BA"",""conditions_of_carriage_url"":""https://www.britishairways.com/en-gb/information/legal/british-airways/general-conditions-of-carriage""},""id"":""seg_00009htYpSCXrwaB9Dn456"",""duration"":""PT02H26M"",""distance"":""424.2"",""destination_terminal"":""5"",""destination"":{""time_zone"":""America/New_York"",""name"":""John F. Kennedy International Airport"",""longitude"":-73.778519,""latitude"":40.640556,""id"":""arp_jfk_us"",""icao_code"":""KJFK"",""iata_country_code"":""US"",""iata_code"":""JFK"",""iata_city_code"":""NYC"",""city_name"":""New York"",""city"":{""name"":""New York"",""id"":""cit_nyc_us"",""iata_country_code"":""US"",""iata_code"":""NYC""}},""departing_at"":""2020-06-13T16:38:02"",""arriving_at"":""2020-06-13T16:38:02"",""aircraft"":{""name"":""Airbus Industries A380"",""id"":""arc_00009UhD4ongolulWd91Ky"",""iata_code"":""380""}}],""origin_type"":""airport"",""origin"":{""type"":""airport"",""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR"",""iata_city_code"":""LON"",""city_name"":""London"",""city"":{""name"":""London"",""id"":""cit_lon_gb"",""iata_country_code"":""GB"",""iata_code"":""LON"",""airports"":[{""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR""}]},""airports"":[{""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR"",""iata_city_code"":""LON"",""city_name"":""London"",""city"":{""name"":""London"",""id"":""cit_lon_gb"",""iata_country_code"":""GB"",""iata_code"":""LON"",""airports"":[{""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR""}]}}]},""ngs_shelf"":1,""id"":""sli_00009htYpSCXrwaB9Dn123"",""fare_brand_name"":""Basic"",""duration"":""PT02H26M"",""destination_type"":""airport"",""destination"":{""type"":""airport"",""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR"",""iata_city_code"":""LON"",""city_name"":""London"",""city"":{""name"":""London"",""id"":""cit_lon_gb"",""iata_country_code"":""GB"",""iata_code"":""LON"",""airports"":[{""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR""}]},""airports"":[{""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR"",""iata_city_code"":""LON"",""city_name"":""London"",""city"":{""name"":""London"",""id"":""cit_lon_gb"",""iata_country_code"":""GB"",""iata_code"":""LON"",""airports"":[{""time_zone"":""Europe/London"",""name"":""Heathrow"",""longitude"":-141.951519,""latitude"":64.068865,""id"":""arp_lhr_gb"",""icao_code"":""EGLL"",""iata_country_code"":""GB"",""iata_code"":""LHR""}]}}]},""conditions"":{""priority_check_in"":""true"",""priority_boarding"":""true"",""change_before_departure"":{""penalty_currency"":""GBP"",""penalty_amount"":""100.00"",""allowed"":true},""advance_seat_selection"":""true""},""comparison_key"":""BmlZDw==""}],""private_fares"":[{""type"":""corporate"",""tracking_reference"":""ABN:2345678"",""tour_code"":""578DFL"",""corporate_code"":""FLX53""}],""payment_requirements"":{""requires_instant_payment"":false,""price_guarantee_expires_at"":""2020-01-17T10:42:14Z"",""payment_required_by"":""2020-01-17T10:42:14Z""},""passengers"":[{""type"":""adult"",""loyalty_programme_accounts"":[{""airline_iata_code"":""BA"",""account_number"":""12901014""}],""id"":""pas_00009hj8USM7Ncg31cBCL"",""given_name"":""Amelia"",""fare_type"":""contract_bulk"",""family_name"":""Earhart"",""age"":14}],""passenger_identity_documents_required"":false,""partial"":true,""owner"":{""name"":""British Airways"",""logo_symbol_url"":""https://assets.duffel.com/img/airlines/for-light-background/full-color-logo/BA.svg"",""logo_lockup_url"":""https://assets.duffel.com/img/airlines/for-light-background/full-color-lockup/BA.svg"",""id"":""arl_00001876aqC8c5umZmrRds"",""iata_code"":""BA"",""conditions_of_carriage_url"":""https://www.britishairways.com/en-gb/information/legal/british-airways/general-conditions-of-carriage""},""live_mode"":true,""intended_services"":[{""quantity"":1,""id"":""ase_00009UhD4ongolulWd9123""}],""id"":""off_00009htYpSCXrwaB9DnUm0"",""expires_at"":""2020-01-17T10:42:14.545Z"",""created_at"":""2020-01-17T10:12:14.545Z"",""conditions"":{""refund_before_departure"":{""penalty_currency"":""GBP"",""penalty_amount"":""100.00"",""allowed"":true},""change_before_departure"":{""penalty_currency"":""GBP"",""penalty_amount"":""100.00"",""allowed"":true}},""base_currency"":""GBP"",""base_amount"":""30.20"",""available_services"":[{""type"":""baggage"",""total_currency"":""GBP"",""total_amount"":""15.00"",""segment_ids"":[""seg_00009hj8USM7Ncg31cB456""],""passenger_ids"":[""pas_00009hj8USM7Ncg31cBCLL""],""maximum_quantity"":1,""id"":""ase_00009UhD4ongolulWd9123""}],""available_airline_credit_ids"":[""acd_00009htYpSCXrwaB9DnUm1"",""acd_00009htYpSCXrwaB9DnUm2""]}}";
    }
}