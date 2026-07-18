using System;
using System.Collections.Generic;

namespace YLproxy.Infrastructure
{
    /// <summary>
    /// Structured log entry for JSON-line output.
    /// </summary>
    public sealed record StructuredLogEntry
    {
        public string Timestamp { get; init; } = DateTime.UtcNow.ToString("O");
        public string Level { get; init; } = "INFO";
        public string Message { get; init; } = string.Empty;
        public string? ExceptionMessage { get; init; }
        public string? ExceptionType { get; init; }
        public string? StackTrace { get; init; }
        public string? CorrelationId { get; init; }
        public Dictionary<string, object?>? Data { get; init; }
    }

    public interface ILogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Fatal(string message);
        void Debug(string message, Exception exception);
        void Info(string message, Exception exception);
        void Warn(string message, Exception exception);
        void Error(string message, Exception exception);
        void Fatal(string message, Exception exception);

        /// <summary>
        /// Writes a structured log entry with optional contextual data.
        /// </summary>
        void Log(LogLevel level, string message, object? data = null);

        /// <summary>
        /// Writes a structured log entry with an exception and optional contextual data.
        /// </summary>
        void Log(LogLevel level, string message, Exception exception, object? data = null);
    }

    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Fatal = 4,
    }
}
