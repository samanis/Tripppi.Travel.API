namespace Tripppi.Travel.API.Services;

/// <summary>
/// Exception thrown when flight search operations fail.
/// </summary>
public sealed class FlightSearchException : Exception
{
    public string ErrorCode { get; }
    public int? StatusCode { get; }
    public bool IsRetryable { get; }

    public FlightSearchException(
        string message, 
        Exception? innerException = null,
        string errorCode = "flight_search_error",
        int? statusCode = null,
        bool isRetryable = false) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        IsRetryable = isRetryable;
    }
}
