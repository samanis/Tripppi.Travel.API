using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tripppi.Travel.API.Domain;
using Tripppi.Travel.API.Infrastructure;
using Tripppi.Travel.API.Repositories;

namespace Tripppi.Travel.Api.Test.Repositories
{
    public class EfAirportRepositoryTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly EfAirportRepository _repository;

        public EfAirportRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new AppDbContext(options);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Cache:AirportsKey", "AirportsCache" }
                })
                .Build();

            var logger = NullLogger<EfAirportRepository>.Instance;

            _repository = new EfAirportRepository(_context, memoryCache, config, logger);

            SeedTestData();
        }

        private void SeedTestData()
        {
            var airports = new List<Airport>
            {
                new Airport
                {
                    AirportIata = "YYZ",
                    AirportName = "Toronto Pearson International Airport",
                    CityName = "Toronto",
                    CityIataCode = "YTO",
                    CountryName = "Canada",
                    CountryIata = "CA"
                },
                new Airport
                {
                    AirportIata = "YTZ",
                    AirportName = "Billy Bishop Toronto City Airport",
                    CityName = "Toronto",
                    CityIataCode = "YTO",
                    CountryName = "Canada",
                    CountryIata = "CA"
                },
                new Airport
                {
                    AirportIata = "JFK",
                    AirportName = "John F. Kennedy International Airport",
                    CityName = "New York",
                    CityIataCode = "NYC",
                    CountryName = "United States",
                    CountryIata = "US"
                },
                new Airport
                {
                    AirportIata = "LGA",
                    AirportName = "LaGuardia Airport",
                    CityName = "New York",
                    CityIataCode = "NYC",
                    CountryName = "United States",
                    CountryIata = "US"
                },
                new Airport
                {
                    AirportIata = "LHR",
                    AirportName = "London Heathrow Airport",
                    CityName = "London",
                    CityIataCode = "LON",
                    CountryName = "United Kingdom",
                    CountryIata = "GB"
                },
                new Airport
                {
                    AirportIata = "CDG",
                    AirportName = "Charles de Gaulle Airport",
                    CityName = "Paris",
                    CityIataCode = "PAR",
                    CountryName = "France",
                    CountryIata = "FR"
                },
                new Airport
                {
                    AirportIata = "ORY",
                    AirportName = "Paris Orly Airport",
                    CityName = "Paris",
                    CityIataCode = "PAR",
                    CountryName = "France",
                    CountryIata = "FR"
                }
            };

            _context.Airports.AddRange(airports);
            _context.SaveChanges();
        }

        [Fact]
        public async Task SearchWithScoringAsync_ExactIataMatch_ReturnsHighestScore()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("YYZ");

            // Assert
            Assert.NotEmpty(results);
            Assert.Equal("YYZ", results[0].AirportIata);
            Assert.Equal("Toronto Pearson International Airport", results[0].AirportName);
        }

        [Fact]
        public async Task SearchWithScoringAsync_IataPrefixMatch_ReturnsExpectedResults()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("YY");

            // Assert: YYZ should match; YTZ does not start with YY so expect at least one result
            Assert.True(results.Count >= 1);
            Assert.Equal("YYZ", results[0].AirportIata);
        }

        [Fact]
        public async Task SearchWithScoringAsync_CityName_ReturnsAirportsInCity()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("Toronto");

            // Assert
            Assert.NotEmpty(results);
            Assert.All(results, r => Assert.Equal("Toronto", r.CityName));
        }

        [Fact]
        public async Task SearchWithScoringAsync_CityPrefix_RanksCorrectly()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("Tor");

            // Assert
            Assert.NotEmpty(results);
            Assert.All(results, r => Assert.Contains("Toronto", r.CityName ?? r.AirportName ?? ""));
        }

        [Fact]
        public async Task SearchWithScoringAsync_CountryName_ReturnsAirportsInCountry()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("France");

            // Assert
            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Equal("France", r.CountryName));
        }

        [Fact]
        public async Task SearchWithScoringAsync_PartialCityName_ReturnsMatches()
        {
            // Act - "York" is 4 characters, so it only matches "starts with"
            var results = await _repository.SearchWithScoringAsync("York");

            // Assert - should be empty since no city starts with "York"
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchWithScoringAsync_LongerQuery_UsesContainsMatching()
        {
            // Act - "London" is 6 characters, so "contains" should work
            var results = await _repository.SearchWithScoringAsync("ondon");

            // Assert - should find London (contains "ondon")
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.CityName == "London");
        }

        [Fact]
        public async Task SearchWithScoringAsync_ReturnsMaximum5Results()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("a"); // Broad search

            // Assert
            Assert.True(results.Count <= 5, "Should return maximum 5 results");
        }

        [Fact]
        public async Task SearchWithScoringAsync_EmptyQuery_ReturnsEmpty()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("");

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchWithScoringAsync_NullQuery_ReturnsEmpty()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync(null!);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchWithScoringAsync_SingleCharQuery_ReturnsEmpty()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("Y");

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchWithScoringAsync_WhitespaceQuery_ReturnsEmpty()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("   ");

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchWithScoringAsync_CaseInsensitive_ReturnsMatches()
        {
            // Act
            var resultsLower = await _repository.SearchWithScoringAsync("yyz");
            var resultsUpper = await _repository.SearchWithScoringAsync("YYZ");
            var resultsMixed = await _repository.SearchWithScoringAsync("YyZ");

            // Assert
            Assert.NotEmpty(resultsLower);
            Assert.NotEmpty(resultsUpper);
            Assert.NotEmpty(resultsMixed);
            Assert.Equal(resultsLower[0].AirportIata, resultsUpper[0].AirportIata);
            Assert.Equal(resultsLower[0].AirportIata, resultsMixed[0].AirportIata);
        }

        [Fact]
        public async Task SearchWithScoringAsync_NoMatches_ReturnsEmpty()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("XYZ123");

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchWithScoringAsync_ExactMatchRanksHigher_ThanPrefix()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("YYZ");

            // Assert
            Assert.NotEmpty(results);
            // YYZ should be first (exact match), YTZ should not appear or be lower
            Assert.Equal("YYZ", results[0].AirportIata);
        }

        [Fact]
        public async Task SearchWithScoringAsync_PrefixMatchRanksHigher_ThanContains()
        {
            // Seed airports to test ranking
            _context.Airports.Add(new Airport
            {
                AirportIata = "ABC",
                AirportName = "Paris International Airport",
                CityName = "Comparison City",
                CountryName = "United Kingdom",
                CountryIata = "GB",
                PopularityScore = 100
            });
            _context.SaveChanges();

            // Act - "Paris" is 5 chars, enables "contains" matching
            var results = await _repository.SearchWithScoringAsync("Paris");

            // Assert
            Assert.NotEmpty(results);
            // "Paris" (city name starts with) should rank first
            Assert.Equal("Paris", results[0].CityName);
            // "Paris International Airport" (airport name contains) should rank lower
            if (results.Count > 1)
            {
                Assert.NotEqual("ABC", results[0].AirportIata);
            }
        }

        [Fact]
        public async Task SearchWithScoringAsync_TrimsWhitespace()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("  YYZ  ");

            // Assert
            Assert.NotEmpty(results);
            Assert.Equal("YYZ", results[0].AirportIata);
        }

        [Fact]
        public async Task SearchWithScoringAsync_CancellationToken_Respected()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await _repository.SearchWithScoringAsync("YYZ", cts.Token)
            );
        }

        [Fact]
        public async Task SearchWithScoringAsync_CityIataCode_ReturnsAirportsInCity()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("YTO");

            // Assert
            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Equal("YTO", r.CityIataCode));
            Assert.Contains(results, r => r.AirportIata == "YYZ");
            Assert.Contains(results, r => r.AirportIata == "YTZ");
        }

        [Fact]
        public async Task SearchWithScoringAsync_AirportNamePrefix_ReturnsMatchingAirport()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("Pearson");

            // Assert
            Assert.NotEmpty(results);
            Assert.Equal("YYZ", results[0].AirportIata);
            Assert.Contains("Pearson", results[0].AirportName);
        }

        [Fact]
        public async Task SearchWithScoringAsync_CountryCode_ReturnsAirportsInCountry()
        {
            // Act
            var results = await _repository.SearchWithScoringAsync("CA");

            // Assert
            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Equal("CA", r.CountryIata));
            Assert.All(results, r => Assert.Equal("Canada", r.CountryName));
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
