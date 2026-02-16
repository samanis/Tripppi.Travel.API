using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tripppi.Travel.API;
using Tripppi.Travel.API.Domain;
using Tripppi.Travel.API.Infrastructure;

namespace Tripppi.Travel.Api.Test.Integration
{
    // Custom factory to properly configure in-memory database
    public class TestWebApplicationFactory : WebApplicationFactory<Tripppi.Travel.API.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove ALL DbContext-related registrations
                var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (dbContextDescriptor != null)
                    services.Remove(dbContextDescriptor);

                var dbContextOptionsDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions));
                if (dbContextOptionsDescriptor != null)
                    services.Remove(dbContextOptionsDescriptor);

                // Remove AppDbContext registration itself
                var appDbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(AppDbContext));
                if (appDbContextDescriptor != null)
                    services.Remove(appDbContextDescriptor);

                // Add in-memory database for testing
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDatabase");
                });
            });

            // Configure test data seeding separately
            builder.ConfigureServices(services =>
            {
                // Build service provider and seed test data
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<AppDbContext>();
                
                // Ensure database is created and seed test data
                db.Database.EnsureCreated();
                SeedTestData(db);
            });
        }

        private static void SeedTestData(AppDbContext context)
        {
            // Clear existing data
            if (context.Airports.Any())
            {
                context.Airports.RemoveRange(context.Airports);
                context.SaveChanges();
            }

            var airports = new[]
            {
                new Airport
                {
                    AirportIata = "AMS",
                    AirportName = "Amsterdam Airport Schiphol",
                    CityName = "Amsterdam",
                    CityIataCode = "AMS",
                    CountryName = "Netherlands",
                    CountryIata = "NL",
                    PopularityScore = 800
                },
                new Airport
                {
                    AirportIata = "YYZ",
                    AirportName = "Toronto Pearson International Airport",
                    CityName = "Toronto",
                    CityIataCode = "YTO",
                    CountryName = "Canada",
                    CountryIata = "CA",
                    PopularityScore = 700
                },
                new Airport
                {
                    AirportIata = "YTZ",
                    AirportName = "Billy Bishop Toronto City Airport",
                    CityName = "Toronto",
                    CityIataCode = "YTO",
                    CountryName = "Canada",
                    CountryIata = "CA",
                    PopularityScore = 200
                },
                new Airport
                {
                    AirportIata = "CDG",
                    AirportName = "Charles de Gaulle Airport",
                    CityName = "Paris",
                    CityIataCode = "PAR",
                    CountryName = "France",
                    CountryIata = "FR",
                    PopularityScore = 1000
                },
                new Airport
                {
                    AirportIata = "LHR",
                    AirportName = "London Heathrow Airport",
                    CityName = "London",
                    CityIataCode = "LON",
                    CountryName = "United Kingdom",
                    CountryIata = "GB",
                    PopularityScore = 1000
                }
            };

            context.Airports.AddRange(airports);
            context.SaveChanges();
        }
    }

    public class AirportSearchIntegrationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public AirportSearchIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task Search_ExactIataMatch_ReturnsOnlyMatchingAirport()
        {
            // Act
            var response = await _client.GetAsync("/api/airports/search?q=AMS");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var airports = await response.Content.ReadFromJsonAsync<Airport[]>();
            
            Assert.NotNull(airports);
            Assert.Single(airports);
            Assert.Equal("AMS", airports[0].AirportIata);
            Assert.Equal("Amsterdam Airport Schiphol", airports[0].AirportName);
        }

        [Fact]
        public async Task Search_CityPrefix_ReturnsAirportsInThatCity()
        {
            // Act
            var response = await _client.GetAsync("/api/airports/search?q=Tor");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var airports = await response.Content.ReadFromJsonAsync<Airport[]>();
            
            Assert.NotNull(airports);
            Assert.Equal(2, airports.Length);
            Assert.All(airports, a => Assert.Equal("Toronto", a.CityName));
        }

        [Fact]
        public async Task Search_NoTextMatch_ReturnsEmpty()
        {
            // Act - high popularity airports should NOT appear without text match
            var response = await _client.GetAsync("/api/airports/search?q=xyz");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var airports = await response.Content.ReadFromJsonAsync<Airport[]>();
            
            Assert.NotNull(airports);
            Assert.Empty(airports);
        }

        [Fact]
        public async Task Search_ShortQuery_OnlyUsesStartsWith()
        {
            // Act - "ame" is 3 chars, should only match starts-with (no contains)
            var response = await _client.GetAsync("/api/airports/search?q=ame");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var airports = await response.Content.ReadFromJsonAsync<Airport[]>();
            
            Assert.NotNull(airports);
            // Should not return Charles de Gaulle even though "Charles" contains no match for "ame"
            // and CDG has high popularity
            Assert.DoesNotContain(airports, a => a.AirportIata == "CDG");
        }

        [Fact]
        public async Task Search_LongerQuery_UsesContainsMatching()
        {
            // Act - "ondon" is 5 chars, enables contains matching
            var response = await _client.GetAsync("/api/airports/search?q=ondon");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var airports = await response.Content.ReadFromJsonAsync<Airport[]>();
            
            Assert.NotNull(airports);
            Assert.NotEmpty(airports);
            Assert.Contains(airports, a => a.CityName == "London");
        }

        [Fact]
        public async Task Search_CaseInsensitive_ReturnsResults()
        {
            // Act
            var responseLower = await _client.GetAsync("/api/airports/search?q=ams");
            var responseUpper = await _client.GetAsync("/api/airports/search?q=AMS");
            var responseMixed = await _client.GetAsync("/api/airports/search?q=AmS");

            // Assert
            Assert.Equal(HttpStatusCode.OK, responseLower.StatusCode);
            Assert.Equal(HttpStatusCode.OK, responseUpper.StatusCode);
            Assert.Equal(HttpStatusCode.OK, responseMixed.StatusCode);

            var airportsLower = await responseLower.Content.ReadFromJsonAsync<Airport[]>();
            var airportsUpper = await responseUpper.Content.ReadFromJsonAsync<Airport[]>();
            var airportsMixed = await responseMixed.Content.ReadFromJsonAsync<Airport[]>();

            Assert.NotNull(airportsLower);
            Assert.NotNull(airportsUpper);
            Assert.NotNull(airportsMixed);
            
            Assert.Single(airportsLower);
            Assert.Single(airportsUpper);
            Assert.Single(airportsMixed);
            
            Assert.Equal(airportsLower[0].AirportIata, airportsUpper[0].AirportIata);
            Assert.Equal(airportsLower[0].AirportIata, airportsMixed[0].AirportIata);
        }

        [Fact]
        public async Task Search_EmptyQuery_ReturnsBadRequest()
        {
            // Act
            var response = await _client.GetAsync("/api/airports/search?q=");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Search_SingleCharQuery_ReturnsBadRequest()
        {
            // Act
            var response = await _client.GetAsync("/api/airports/search?q=A");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Search_MaximumFiveResults_ReturnsLimitedSet()
        {
            // Act - broad search that could return many results
            var response = await _client.GetAsync("/api/airports/search?q=or");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var airports = await response.Content.ReadFromJsonAsync<Airport[]>();
            
            Assert.NotNull(airports);
            Assert.True(airports.Length <= 5, "Should return maximum 5 results");
        }

        [Fact]
        public async Task Search_HigherRelevanceRanksFirst_OverPopularity()
        {
            // Act - search for "Tor" should prioritize Toronto airports (prefix match)
            // over popular airports without text match
            var response = await _client.GetAsync("/api/airports/search?q=Tor");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var airports = await response.Content.ReadFromJsonAsync<Airport[]>();
            
            Assert.NotNull(airports);
            Assert.NotEmpty(airports);
            
            // First result should be Toronto (text match) not London/Paris (high popularity, no match)
            Assert.Equal("Toronto", airports[0].CityName);
            
            // Should not contain airports without text match
            Assert.DoesNotContain(airports, a => a.AirportIata == "CDG");
            Assert.DoesNotContain(airports, a => a.AirportIata == "LHR");
        }
    }
}
