using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Tripppi.Travel.API.Middleware;

namespace Tripppi.Travel.Api.Test.Middleware
{
    public class GlobalExceptionHandlerMiddlewareTests
    {
        [Fact]
        public async Task Returns500AndGenericMessage_InProduction()
        {
            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder.UseTestServer();
                    webBuilder.UseEnvironment("Production");
                    webBuilder.Configure(app =>
                    {
                        app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
                        app.Run(_ => throw new InvalidOperationException("Test error"));
                    });
                })
                .StartAsync();

            var client = host.GetTestClient();
            var response = await client.GetAsync("/");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var json = JsonDocument.Parse(body);
            Assert.Equal(500, json.RootElement.GetProperty("statusCode").GetInt32());
            Assert.Equal("An error occurred while processing your request.", json.RootElement.GetProperty("message").GetString());
            // In production, 'details' property exists but is null
            Assert.True(json.RootElement.TryGetProperty("details", out var detailsProp));
            Assert.True(detailsProp.ValueKind == JsonValueKind.Null);
        }

        [Fact]
        public async Task Returns500AndExceptionDetails_InDevelopment()
        {
            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder.UseTestServer();
                    webBuilder.UseEnvironment("Development");
                    webBuilder.Configure(app =>
                    {
                        app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
                        app.Run(_ => throw new InvalidOperationException("Dev error"));
                    });
                })
                .StartAsync();

            var client = host.GetTestClient();
            var response = await client.GetAsync("/");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var json = JsonDocument.Parse(body);
            Assert.Equal(500, json.RootElement.GetProperty("statusCode").GetInt32());
            Assert.Equal("An error occurred while processing your request.", json.RootElement.GetProperty("message").GetString());
            Assert.Equal("Dev error", json.RootElement.GetProperty("details").GetString());
            Assert.Equal("InvalidOperationException", json.RootElement.GetProperty("exceptionType").GetString());
            Assert.True(json.RootElement.TryGetProperty("stackTrace", out _));
        }
    }
}
