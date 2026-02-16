using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Tripppi.Travel.API.Controllers;
using Tripppi.Travel.API.Domain;
using Tripppi.Travel.API.Services;
using Xunit;

namespace Tripppi.Travel.Api.Test.Controllers
{
    public class AirportsControllerTests
    {
        private readonly Mock<ILocationService> _mockLocationService;
        private readonly Mock<ILogger<AirportsController>> _mockLogger;
        private readonly AirportsController _controller;

        public AirportsControllerTests()
        {
            _mockLocationService = new Mock<ILocationService>();
            _mockLogger = new Mock<ILogger<AirportsController>>();
            _controller = new AirportsController(_mockLocationService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Search_WithValidQuery_ReturnsOkWithAirports()
        {
            // Arrange
            var query = "tor";
            var expectedAirports = new List<Airport>
            {
                new Airport
                {
                    AirportIata = "YYZ",
                    AirportName = "Toronto Pearson International Airport",
                    CityName = "Toronto",
                    CountryName = "Canada"
                }
            };

            _mockLocationService
                .Setup(x => x.SearchAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedAirports);

            // Act
            var result = await _controller.Search(query, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var airports = Assert.IsAssignableFrom<IReadOnlyList<Airport>>(okResult.Value);
            Assert.Single(airports);
            Assert.Equal("YYZ", airports[0].AirportIata);
        }

        [Fact]
        public async Task Search_WithNoResults_ReturnsNotFound()
        {
            // Arrange
            var query = "xyz123";
            var emptyList = new List<Airport>();

            _mockLocationService
                .Setup(x => x.SearchAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptyList);

            // Act
            var result = await _controller.Search(query, CancellationToken.None);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var errorResponse = notFoundResult.Value;
            Assert.NotNull(errorResponse);
        }

        [Fact]
        public async Task Search_WithNullResults_ReturnsNotFound()
        {
            // Arrange
            var query = "test";

            _mockLocationService
                .Setup(x => x.SearchAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<Airport>)null!);

            // Act
            var result = await _controller.Search(query, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Search_WithInvalidQuery_ReturnsBadRequest(string invalidQuery)
        {
            // Act
            var result = await _controller.Search(invalidQuery, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = badRequestResult.Value;
            Assert.NotNull(errorResponse);
        }

        [Fact]
        public async Task Search_WithMultipleResults_ReturnsOkWithAllAirports()
        {
            // Arrange
            var query = "lon";
            var expectedAirports = new List<Airport>
            {
                new Airport
                {
                    AirportIata = "LHR",
                    AirportName = "London Heathrow Airport",
                    CityName = "London",
                    CountryName = "United Kingdom"
                },
                new Airport
                {
                    AirportIata = "LGW",
                    AirportName = "London Gatwick Airport",
                    CityName = "London",
                    CountryName = "United Kingdom"
                },
                new Airport
                {
                    AirportIata = "LCY",
                    AirportName = "London City Airport",
                    CityName = "London",
                    CountryName = "United Kingdom"
                }
            };

            _mockLocationService
                .Setup(x => x.SearchAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedAirports);

            // Act
            var result = await _controller.Search(query, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var airports = Assert.IsAssignableFrom<IReadOnlyList<Airport>>(okResult.Value);
            Assert.Equal(3, airports.Count);
            Assert.All(airports, a => Assert.Equal("London", a.CityName));
        }

        [Fact]
        public async Task Search_WhenServiceThrowsException_RethrowsException()
        {
            // Arrange
            var query = "test";
            var expectedException = new InvalidOperationException("Database connection failed");

            _mockLocationService
                .Setup(x => x.SearchAsync(query, It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _controller.Search(query, CancellationToken.None));

            Assert.Equal("Database connection failed", exception.Message);
        }

        [Fact]
        public async Task Search_WithCancellationToken_PassesToService()
        {
            // Arrange
            var query = "ams";
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var airports = new List<Airport>();

            _mockLocationService
                .Setup(x => x.SearchAsync(query, cancellationToken))
                .ReturnsAsync(airports);

            // Act
            await _controller.Search(query, cancellationToken);

            // Assert
            _mockLocationService.Verify(
                x => x.SearchAsync(query, cancellationToken),
                Times.Once);
        }

        [Fact]
        public async Task Search_LogsSearchQuery()
        {
            // Arrange
            var query = "par";
            var airports = new List<Airport>
            {
                new Airport { AirportIata = "CDG", AirportName = "Charles de Gaulle", CityName = "Paris" }
            };

            _mockLocationService
                .Setup(x => x.SearchAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(airports);

            // Act
            await _controller.Search(query, CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(query)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Search_WhenNoResultsFound_LogsInformation()
        {
            // Arrange
            var query = "notfound";
            var emptyList = new List<Airport>();

            _mockLocationService
                .Setup(x => x.SearchAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptyList);

            // Act
            await _controller.Search(query, CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No airports found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Search_WhenExceptionOccurs_LogsError()
        {
            // Arrange
            var query = "test";
            var expectedException = new Exception("Test exception");

            _mockLocationService
                .Setup(x => x.SearchAsync(query, It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _controller.Search(query, CancellationToken.None));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error searching airports")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
