using System.Text.Json;

namespace YLproxy.Infrastructure;

/// <summary>
/// Static helper class for structured logging with context enrichment.
/// Provides convenience methods for writing JSON-line log entries
/// with optional correlation IDs and contextual data dictionaries.
/// </summary>
public static class Logger
{
    private static ILogger? _defaultLogger;
    private static readonly object _lock = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Gets or sets the default logger instance. If not set, falls back to
    /// <see cref="LoggerFactory.CreateLogger"/>.
    /// </summary>
    public static ILogger Default
    {
        get
        {
            if (_defaultLogger is null)
            {
                lock (_lock)
                {
                    _defaultLogger ??= LoggerFactory.CreateLogger();
                }
            }
            return _defaultLogger;
        }
        set => _defaultLogger = value;
    }

    /// <summary>
    /// Logs an informational message with optional structured context.
    /// </summary>
    public static void Info(string message, object? context = null)
    {
        Default.Log(LogLevel.Info, message, context);
    }

    /// <summary>
    /// Logs a warning message with optional structured context.
    /// </summary>
    public static void Warn(string message, object? context = null)
    {
        Default.Log(LogLevel.Warn, message, context);
    }

    /// <summary>
    /// Logs an error message with optional structured context.
    /// </summary>
    public static void Error(string message, object? context = null)
    {
        Default.Log(LogLevel.Error, message, context);
    }

    /// <summary>
    /// Logs a debug message with optional structured context.
    /// </summary>
    public static void Debug(string message, object? context = null)
    {
        Default.Log(LogLevel.Debug, message, context);
    }

    /// <summary>
    /// Logs an error message with exception details and optional structured context.
    /// </summary>
    public static void Error(string message, Exception exception, object? context = null)
    {
        Default.Log(LogLevel.Error, message, exception, context);
    }

    /// <summary>
    /// Logs a fatal message with optional structured context.
    /// </summary>
    public static void Fatal(string message, object? context = null)
    {
        Default.Log(LogLevel.Fatal, message, context);
    }

    /// <summary>
    /// Logs a fatal message with exception details and optional structured context.
    /// </summary>
    public static void Fatal(string message, Exception exception, object? context = null)
    {
        Default.Log(LogLevel.Fatal, message, exception, context);
    }

    /// <summary>
    /// Creates a structured log entry dictionary from key-value pairs.
    /// </summary>
    public static Dictionary<string, object?> CreateContext(params (string Key, object? Value)[] entries)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries)
        {
            dict[key] = value;
        }
        return dict;
    }

    /// <summary>
    /// Serializes an object to a JSON string for structured log context.
    /// </summary>
    public static string ToJson(object? value)
    {
        if (value is null) return "null";
        try
        {
            return JsonSerializer.Serialize(value, _jsonOptions);
        }
        catch
        {
            return value.ToString() ?? "null";
        }
    }
}

