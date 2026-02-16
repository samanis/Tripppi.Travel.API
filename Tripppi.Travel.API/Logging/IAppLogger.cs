using System;

namespace Tripppi.Travel.API.Logging
{
    /// <summary>
    /// Platform-agnostic logging abstraction.
    /// Wraps ILogger<T> to provide flexibility for future logging provider changes.
    /// </summary>
    public interface IAppLogger<T>
    {
        void LogDebug(string message, params object[] args);
        void LogInformation(string message, params object[] args);
        void LogWarning(string message, params object[] args);
        void LogWarning(Exception exception, string message, params object[] args);
        void LogError(string message, params object[] args);
        void LogError(Exception exception, string message, params object[] args);
        void LogCritical(Exception exception, string message, params object[] args);
    }
}
