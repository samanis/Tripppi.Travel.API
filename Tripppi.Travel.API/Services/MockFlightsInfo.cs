using Tripppi.Travel.API.appService;

namespace Tripppi.Travel.API.Services
{
    /// <summary>
    /// Mock implementation of IFlightsInfo for development/testing.
    /// Returns empty/placeholder responses until real Duffel API client is configured.
    /// </summary>
    internal sealed class MockFlightsInfo : IFlightsInfo
    {
        public Task<string> SearchOffersAsync(object searchRequest, CancellationToken cancellationToken = default)
        {
            // Return empty offer search response
            return Task.FromResult(@"{""data"":{""id"":""mock_req_123"",""offers"":[]}}");
        }

        public Task<string> RepriceOfferAsync(
            string offerId, 
            object repriceRequest, 
            string? idempotencyKey = null, 
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(@"{""data"":{""id"":""mock_offer_123"",""total_amount"":""0.00""}}");
        }

        public Task<string> CreateOrderAsync(
            object orderRequest, 
            string? idempotencyKey = null, 
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(@"{""data"":{""id"":""mock_order_123"",""booking_reference"":""MOCK123""}}");
        }

        public Task<string> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(@"{""data"":{""id"":""mock_order_123"",""status"":""pending""}}");
        }

        public Task<bool> VerifyWebhookSignatureAsync(string signatureHeaderValue, string payload)
        {
            // Mock always returns true for development
            return Task.FromResult(true);
        }
    }
}
