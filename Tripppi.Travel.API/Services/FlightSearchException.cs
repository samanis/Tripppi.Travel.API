namespace Tripppi.Travel.API.Services
{
    /// <summary>
    /// Exception thrown when flight search operations fail.
    /// Provides structured error information for logging and user feedback.
    /// </summary>
    public class FlightSearchException : Exception
    {
        /// <summary>
        /// Vendor-specific error code (e.g., "rate_limit_exceeded", "airline_error").
        /// </summary>
        public string? ErrorCode { get; init; }

        /// <summary>
        /// HTTP status code from vendor API (if applicable).
        /// </summary>
        public int? StatusCode { get; init; }

        /// <summary>
        /// Whether the operation can be retried (transient error).
        /// </summary>
        public bool IsRetryable { get; init; }

        public FlightSearchException(string message) : base(message)
        {
        }

        public FlightSearchException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        public FlightSearchException(
            string message, 
            string? errorCode = null, 
            int? statusCode = null, 
            bool isRetryable = false) 
            : base(message)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
            IsRetryable = isRetryable;
        }

        public FlightSearchException(
            string message, 
            Exception innerException, 
            string? errorCode = null, 
            int? statusCode = null, 
            bool isRetryable = false) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
            IsRetryable = isRetryable;
        }
    }
}
