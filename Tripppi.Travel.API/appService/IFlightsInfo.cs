using System.Threading;
using System.Threading.Tasks;

namespace Tripppi.Travel.API.appService
{
    /// <summary>
    /// Abstraction for flight data / booking provider used by the application.
    /// Implementations should avoid exposing raw sensitive data and should be idempotent-safe.
    /// </summary>
    public interface IFlightsInfo
    {
        /// <summary>
        /// Search offers / fares for a given request payload.
        /// Payload is intentionally untyped here to keep the interface implementation-friendly;
        /// callers should use DTOs or JsonDocument as appropriate.
        /// </summary>
        Task<string> SearchOffersAsync(object searchRequest, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reprice or lock an offer prior to booking. Accepts an optional idempotency key.
        /// </summary>
        Task<string> RepriceOfferAsync(string offerId, object repriceRequest, string? idempotencyKey = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create an order (booking) with the provider. Accepts an optional idempotency key.
        /// </summary>
        Task<string> CreateOrderAsync(object orderRequest, string? idempotencyKey = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetch order details from the provider by provider order id.
        /// </summary>
        Task<string> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verify an incoming webhook payload using the configured secret.
        /// Implementations decide the expected signature header format and verification algorithm.
        /// Returns true when the signature is valid.
        /// </summary>
        Task<bool> VerifyWebhookSignatureAsync(string signatureHeaderValue, string payload);
    }
}