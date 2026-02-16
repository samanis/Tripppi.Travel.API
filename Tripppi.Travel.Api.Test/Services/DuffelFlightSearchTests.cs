using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Tripppi.Travel.API.appService;
using Tripppi.Travel.API.Services;
using Tripppi.Travel.API.Services.DTOs;
using Xunit;

namespace Tripppi.Travel.Api.Test.Services
{
    public class DuffelFlightSearchTests
    {
        private readonly Mock<IFlightsInfo> _mockDuffelClient;
        private readonly Mock<ILogger<DuffelFlightSearch>> _mockLogger;
        private readonly DuffelFlightSearch _service;

        public DuffelFlightSearchTests()
        {
            _mockDuffelClient = new Mock<IFlightsInfo>();
            _mockLogger = new Mock<ILogger<DuffelFlightSearch>>();
            _service = new DuffelFlightSearch(_mockDuffelClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task SearchFlightsAsync_ValidRequest_ReturnsResults()
        {
            // Arrange
            var request = CreateValidSearchRequest();
            var mockResponse = GetMockDuffelResponse();
            
            _mockDuffelClient
                .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse);

            // Act
            var results = await _service.SearchFlightsAsync(request);

            // Assert
            Assert.NotNull(results);
            Assert.NotEmpty(results);
            Assert.Equal("off_123456", results[0].OfferId);
            Assert.Equal(450.50m, results[0].TotalAmount);
            Assert.Equal("USD", results[0].Currency);
            Assert.Equal(2, results[0].Legs.Count); // Round trip
            
            _mockDuffelClient.Verify(
                x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task SearchFlightsAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _service.SearchFlightsAsync(null!, CancellationToken.None));
        }

        [Fact]
        public async Task SearchFlightsAsync_NoOffers_ReturnsEmptyList()
        {
            // Arrange
            var request = CreateValidSearchRequest();
            var emptyResponse = @"{""data"":{""id"":""req_123"",""offers"":[]}}";
            
            _mockDuffelClient
                .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptyResponse);

            // Act
            var results = await _service.SearchFlightsAsync(request);

            // Assert
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchFlightsAsync_HttpRequestException_ThrowsFlightSearchException()
        {
            // Arrange
            var request = CreateValidSearchRequest();
            var httpException = new HttpRequestException(
                "Service unavailable", 
                null, 
                System.Net.HttpStatusCode.ServiceUnavailable);
            
            _mockDuffelClient
                .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(httpException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FlightSearchException>(
                () => _service.SearchFlightsAsync(request));
            
            Assert.Equal("duffel_api_error", exception.ErrorCode);
            Assert.Equal(503, exception.StatusCode);
            Assert.True(exception.IsRetryable);
        }

        [Fact]
        public async Task SearchFlightsAsync_InvalidJson_ThrowsFlightSearchException()
        {
            // Arrange
            var request = CreateValidSearchRequest();
            var invalidJson = "{ invalid json }";
            
            _mockDuffelClient
                .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(invalidJson);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FlightSearchException>(
                () => _service.SearchFlightsAsync(request));
            
            Assert.Equal("invalid_response", exception.ErrorCode);
            Assert.False(exception.IsRetryable);
        }

        [Fact]
        public async Task SearchFlightsAsync_RoundTrip_MapsLegsCorrectly()
        {
            // Arrange
            var request = new FlightSearchRequest
            {
                Legs = new List<FlightLegRequest>
                {
                    new() { OriginIataCode = "YYZ", DestinationIataCode = "LHR", DepartureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)) },
                    new() { OriginIataCode = "LHR", DestinationIataCode = "YYZ", DepartureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(37)) }
                },
                Passengers = new List<PassengerRequest> { new() { Type = PassengerType.Adult } },
                CabinClass = CabinClass.Economy
            };
            
            var mockResponse = GetMockDuffelResponse();
            _mockDuffelClient
                .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse);

            // Act
            var results = await _service.SearchFlightsAsync(request);

            // Assert
            Assert.Equal(2, results[0].Legs.Count);
            Assert.Equal("YYZ", results[0].Legs[0].OriginIataCode);
            Assert.Equal("LHR", results[0].Legs[0].DestinationIataCode);
            Assert.Equal("LHR", results[0].Legs[1].OriginIataCode);
            Assert.Equal("YYZ", results[0].Legs[1].DestinationIataCode);
        }

        [Fact]
        public async Task SearchFlightsAsync_ConnectingFlight_MapsSegmentsCorrectly()
        {
            // Arrange
            var request = CreateValidSearchRequest();
            var mockResponse = GetMockDuffelResponse();
            
            _mockDuffelClient
                .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse);

            // Act
            var results = await _service.SearchFlightsAsync(request);

            // Assert
            var firstLeg = results[0].Legs[0];
            Assert.Equal(2, firstLeg.Segments.Count); // Connection via ORD
            Assert.Equal("YYZ", firstLeg.Segments[0].OriginIataCode);
            Assert.Equal("ORD", firstLeg.Segments[0].DestinationIataCode);
            Assert.Equal("ORD", firstLeg.Segments[1].OriginIataCode);
            Assert.Equal("LHR", firstLeg.Segments[1].DestinationIataCode);
        }

        [Fact]
        public async Task SearchFlightsAsync_MapsFareConditionsCorrectly()
        {
            // Arrange
            var request = CreateValidSearchRequest();
            var mockResponse = GetMockDuffelResponse();
            
            _mockDuffelClient
                .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse);

            // Act
            var results = await _service.SearchFlightsAsync(request);

            // Assert
            var conditions = results[0].Conditions;
            Assert.True(conditions.ChangeBeforeDepartureAllowed);
            Assert.Equal(50m, conditions.ChangeBeforeDeparturePenaltyAmount);
            Assert.Equal("USD", conditions.ChangeBeforeDeparturePenaltyCurrency);
            Assert.False(conditions.RefundBeforeDepartureAllowed);
        }

        [Fact]
        public async Task SearchFlightsAsync_MapsBaggageCorrectly()
        {
            // Arrange
            var request = CreateValidSearchRequest();
            var mockResponse = GetMockDuffelResponse();
            
            _mockDuffelClient
                .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse);

            // Act
            var results = await _service.SearchFlightsAsync(request);

            // Assert
            var passenger = results[0].Passengers[0];
            Assert.Equal(2, passenger.Baggages.Count);
            Assert.Contains(passenger.Baggages, b => b.Type == BaggageType.CarryOn && b.Quantity == 1);
            Assert.Contains(passenger.Baggages, b => b.Type == BaggageType.Checked && b.Quantity == 2);
        }

        [Fact]
        public async Task SearchFlightsAsync_WithExcludedAirlines_PassesToDuffel()
        {
            // Arrange
            var request = new FlightSearchRequest
            {
                Legs = new List<FlightLegRequest>
                {
                    new() { OriginIataCode = "YYZ", DestinationIataCode = "LHR", DepartureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)) }
                },
                Passengers = new List<PassengerRequest> { new() { Type = PassengerType.Adult } },
                CabinClass = CabinClass.Economy,
                ExcludedAirlines = new List<string> { "AA", "UA" }
            };
            
            var mockResponse = GetMockDuffelResponse();
            _mockDuffelClient
                .Setup(x => x.SearchOffersAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse);

            // Act
            await _service.SearchFlightsAsync(request);

            // Assert
            _mockDuffelClient.Verify(
                x => x.SearchOffersAsync(
                    It.Is<object>(o => o.ToString()!.Contains("exclude")), 
                    It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        #region Helper Methods

        private static FlightSearchRequest CreateValidSearchRequest()
        {
            return new FlightSearchRequest
            {
                Legs = new List<FlightLegRequest>
                {
                    new() 
                    { 
                        OriginIataCode = "YYZ", 
                        DestinationIataCode = "LHR", 
                        DepartureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30))
                    },
                    new() 
                    { 
                        OriginIataCode = "LHR", 
                        DestinationIataCode = "YYZ", 
                        DepartureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(37))
                    }
                },
                Passengers = new List<PassengerRequest>
                {
                    new() { Type = PassengerType.Adult, Age = 30 }
                },
                CabinClass = CabinClass.Economy,
                MaxConnections = 1,
                ReturnAllOffers = true
            };
        }

        private static string GetMockDuffelResponse()
        {
            return @"{
  ""data"": {
    ""id"": ""req_123456"",
    ""offers"": [
      {
        ""id"": ""off_123456"",
        ""total_amount"": ""450.50"",
        ""total_currency"": ""USD"",
        ""base_amount"": ""380.00"",
        ""tax_amount"": ""70.50"",
        ""expires_at"": ""2024-12-31T23:59:59Z"",
        ""payment_requirements"": {
          ""requires_instant_payment"": true
        },
        ""slices"": [
          {
            ""origin"": {
              ""iata_code"": ""YYZ"",
              ""name"": ""Toronto Pearson International Airport"",
              ""city_name"": ""Toronto""
            },
            ""destination"": {
              ""iata_code"": ""LHR"",
              ""name"": ""London Heathrow Airport"",
              ""city_name"": ""London""
            },
            ""duration"": ""PT10H30M"",
            ""segments"": [
              {
                ""operating_carrier"": { ""iata_code"": ""AC"", ""name"": ""Air Canada"" },
                ""marketing_carrier"": { ""iata_code"": ""AC"", ""name"": ""Air Canada"" },
                ""marketing_carrier_flight_number"": ""878"",
                ""aircraft"": { ""name"": ""Boeing 787-9"" },
                ""origin"": { ""iata_code"": ""YYZ"", ""terminal"": ""1"" },
                ""destination"": { ""iata_code"": ""ORD"", ""terminal"": ""1"" },
                ""departing_at"": ""2024-12-15T08:00:00Z"",
                ""arriving_at"": ""2024-12-15T09:30:00Z"",
                ""duration"": ""PT1H30M""
              },
              {
                ""operating_carrier"": { ""iata_code"": ""BA"", ""name"": ""British Airways"" },
                ""marketing_carrier"": { ""iata_code"": ""BA"", ""name"": ""British Airways"" },
                ""marketing_carrier_flight_number"": ""296"",
                ""aircraft"": { ""name"": ""Boeing 777-300ER"" },
                ""origin"": { ""iata_code"": ""ORD"", ""terminal"": ""5"" },
                ""destination"": { ""iata_code"": ""LHR"", ""terminal"": ""5"" },
                ""departing_at"": ""2024-12-15T13:00:00Z"",
                ""arriving_at"": ""2024-12-16T02:30:00Z"",
                ""duration"": ""PT7H30M""
              }
            ]
          },
          {
            ""origin"": {
              ""iata_code"": ""LHR"",
              ""name"": ""London Heathrow Airport"",
              ""city_name"": ""London""
            },
            ""destination"": {
              ""iata_code"": ""YYZ"",
              ""name"": ""Toronto Pearson International Airport"",
              ""city_name"": ""Toronto""
            },
            ""duration"": ""PT8H15M"",
            ""segments"": [
              {
                ""operating_carrier"": { ""iata_code"": ""AC"", ""name"": ""Air Canada"" },
                ""marketing_carrier"": { ""iata_code"": ""AC"", ""name"": ""Air Canada"" },
                ""marketing_carrier_flight_number"": ""855"",
                ""aircraft"": { ""name"": ""Boeing 787-9"" },
                ""origin"": { ""iata_code"": ""LHR"", ""terminal"": ""2"" },
                ""destination"": { ""iata_code"": ""YYZ"", ""terminal"": ""1"" },
                ""departing_at"": ""2024-12-22T10:00:00Z"",
                ""arriving_at"": ""2024-12-22T13:15:00Z"",
                ""duration"": ""PT8H15M""
              }
            ]
          }
        ],
        ""passengers"": [
          {
            ""type"": ""adult"",
            ""cabin_class"": ""economy"",
            ""cabin_class_marketing_name"": ""Economy Standard"",
            ""fare_basis_code"": ""KRTXUS"",
            ""baggages"": [
              { ""type"": ""carry_on"", ""quantity"": 1 },
              { ""type"": ""checked"", ""quantity"": 2 }
            ]
          }
        ],
        ""conditions"": {
          ""change_before_departure"": {
            ""allowed"": true,
            ""penalty_amount"": ""50.00"",
            ""penalty_currency"": ""USD""
          },
          ""refund_before_departure"": {
            ""allowed"": false
          },
          ""change_after_departure"": {
            ""allowed"": false
          },
          ""refund_after_departure"": {
            ""allowed"": false
          }
        },
        ""owner"": {
          ""iata_code"": ""AC"",
          ""name"": ""Air Canada"",
          ""logo_symbol_url"": ""https://assets.duffel.com/img/airlines/for-light-background/full-color-logo/AC.svg""
        }
      }
    ]
  }
}";
        }

        #endregion
    }
}
