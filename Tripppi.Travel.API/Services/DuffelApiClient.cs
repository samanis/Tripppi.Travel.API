using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Tripppi.Travel.API.appService;
using Tripppi.Travel.API.Configuration;

namespace Tripppi.Travel.API.Services
{
    /// <summary>
    /// Real implementation of IFlightsInfo that calls Duffel API v2.
    /// Handles authentication, HTTP requests, error responses, and idempotency.
    /// </summary>
    public sealed class DuffelApiClient : IFlightsInfo
    {
        private readonly HttpClient _httpClient;
        private readonly DuffelOptions _options;
        private readonly ILogger<DuffelApiClient> _logger;

        public DuffelApiClient(
            HttpClient httpClient,
            IOptions<DuffelOptions> options,
            ILogger<DuffelApiClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure HTTP client with Duffel authentication
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("Duffel-Version", _options.ApiVersion);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _logger.LogInformation(
                "DuffelApiClient initialized: BaseUrl={BaseUrl}, Version={Version}, TestMode={IsTest}",
                _options.BaseUrl,
                _options.ApiVersion,
                _options.IsTestMode);
        }

        public async Task<string> SearchOffersAsync(
            object searchRequest, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Sending offer request to Duffel API");

            try
            {
                // Serialize with snake_case naming (Duffel's convention)
                var json = JsonSerializer.Serialize(searchRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                _logger.LogDebug("Duffel request payload: {Payload}", json);

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    "/air/offer_requests", 
                    content, 
                    cancellationToken);

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Duffel API error: {StatusCode} - {Response}", 
                        response.StatusCode, 
                        responseBody);
                    
                    throw new HttpRequestException(
                        $"Duffel API returned {response.StatusCode}: {responseBody}",
                        null,
                        response.StatusCode);
                }

                _logger.LogInformation(
                    "Duffel API search completed: {StatusCode}, Response length: {Length}",
                    response.StatusCode,
                    responseBody.Length);

                return responseBody;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to call Duffel search API");
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Duffel search request timed out or was cancelled");
                throw new HttpRequestException("Request to Duffel timed out", ex);
            }
        }

        public async Task<string> RepriceOfferAsync(
            string offerId, 
            object repriceRequest, 
            string? idempotencyKey = null, 
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(offerId);

            _logger.LogInformation(
                "Repricing offer {OfferId} with Duffel API (IdempotencyKey: {IdempotencyKey})",
                offerId,
                idempotencyKey ?? "none");

            var json = JsonSerializer.Serialize(repriceRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var request = new HttpRequestMessage(HttpMethod.Post, "/air/partial_offer_requests")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // Add idempotency key if provided (critical for preventing duplicate repricing)
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                request.Headers.Add("Idempotency-Key", idempotencyKey);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Duffel reprice error: {StatusCode} - {Response}",
                    response.StatusCode,
                    responseBody);
                
                throw new HttpRequestException(
                    $"Duffel reprice failed: {response.StatusCode}",
                    null,
                    response.StatusCode);
            }

            _logger.LogInformation("Duffel reprice completed: {StatusCode}", response.StatusCode);
            return responseBody;
        }

        public async Task<string> CreateOrderAsync(
            object orderRequest, 
            string? idempotencyKey = null, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Creating order with Duffel API (IdempotencyKey: {IdempotencyKey})",
                idempotencyKey ?? "none");

            var json = JsonSerializer.Serialize(orderRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var request = new HttpRequestMessage(HttpMethod.Post, "/air/orders")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // Add idempotency key if provided (CRITICAL for preventing duplicate bookings)
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                request.Headers.Add("Idempotency-Key", idempotencyKey);
            }
            else
            {
                _logger.LogWarning("CreateOrderAsync called without idempotency key - risk of duplicate bookings!");
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Duffel create order error: {StatusCode} - {Response}",
                    response.StatusCode,
                    responseBody);
                
                throw new HttpRequestException(
                    $"Duffel order creation failed: {response.StatusCode}",
                    null,
                    response.StatusCode);
            }

            _logger.LogInformation("Duffel order created: {StatusCode}", response.StatusCode);
            return responseBody;
        }

        public async Task<string> GetOrderAsync(
            string orderId, 
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

            _logger.LogInformation("Retrieving order {OrderId} from Duffel API", orderId);

            var response = await _httpClient.GetAsync(
                $"/air/orders/{orderId}", 
                cancellationToken);
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Duffel get order error: {StatusCode} - {Response}",
                    response.StatusCode,
                    responseBody);
                
                throw new HttpRequestException(
                    $"Failed to get order from Duffel: {response.StatusCode}",
                    null,
                    response.StatusCode);
            }

            _logger.LogInformation("Duffel order retrieved: {StatusCode}", response.StatusCode);
            return responseBody;
        }

        public Task<bool> VerifyWebhookSignatureAsync(
            string signatureHeaderValue, 
            string payload)
        {
            // TODO: Implement HMAC-SHA256 signature verification
            // See: https://duffel.com/docs/api/overview/webhooks#verifying-webhook-signatures
            // 
            // Algorithm:
            // 1. Extract timestamp and signature from header (format: "t=<timestamp>,v1=<signature>")
            // 2. Construct signed payload: "<timestamp>.<payload>"
            // 3. Compute HMAC-SHA256(webhook_secret, signed_payload)
            // 4. Compare computed signature with received signature (constant-time comparison)
            // 5. Verify timestamp is within 5 minutes (replay attack prevention)

            _logger.LogWarning(
                "Webhook signature verification not yet implemented - accepting all webhooks (INSECURE)");
            
            return Task.FromResult(true);
        }
    }
}
