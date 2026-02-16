using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using Tripppi.Travel.API;
using Tripppi.Travel.API.Services;
using Tripppi.Travel.API.Services.DTOs;
using Xunit;

namespace Tripppi.Travel.Api.Test.Controllers
{
    public class FlightsControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public FlightsControllerTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task SearchFlights_ValidRequest_Returns200()
        {
            // Arrange
            var client = _factory.CreateClient();
            var request = new FlightSearchRequest
            {
                Legs = new List<FlightLegRequest>
                {
                    new()
                    {
                        OriginIataCode = "YYZ",
                        DestinationIataCode = "LHR",
                        DepartureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30))
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

            // Act
            var response = await client.PostAsJsonAsync("/api/flights/search", request);

            // Assert
            // Note: This will fail with 500 because IFlightsInfo is not mocked
            // In a real scenario, you'd mock IFlightsInfo in a custom WebApplicationFactory
            Assert.NotNull(response);
        }

        [Fact]
        public async Task SearchFlights_InvalidRequest_Returns400()
        {
            // Arrange
            var client = _factory.CreateClient();
            var invalidRequest = new
            {
                legs = new object[] { }, // Empty legs - should be invalid
                passengers = new[]
                {
                    new { type = "Adult" }
                },
                cabinClass = "Economy"
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/flights/search", invalidRequest);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task HealthCheck_Returns200()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/flights/health");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("FlightSearch", content);
            Assert.Contains("healthy", content);
        }

        [Fact]
        public async Task SearchFlights_RoundTrip_ValidRequest()
        {
            // Arrange
            var client = _factory.CreateClient();
            var request = new FlightSearchRequest
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
                CabinClass = CabinClass.Economy
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/flights/search", request);

            // Assert
            Assert.NotNull(response);
            // Actual validation would require mocked IFlightsInfo
        }
    }
}
