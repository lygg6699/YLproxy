namespace YLproxy.GUI;

public enum LogLevel { Debug, Info, Warn, Error, Fatal }

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public LogLevel Level { get; init; } = LogLevel.Info;
    public string Text { get; init; } = string.Empty;

    /// <summary>For display: "[HH:mm:ss] message" format.</summary>
    public override string ToString() => $"[{Timestamp:HH:mm:ss}] {Text}";

    public static LogEntry FromRawString(string raw)
    {
        var level = LogLevel.Info;
        var upper = raw.ToUpperInvariant();
        if (upper.Contains("[FATAL]") || upper.Contains("FATAL]")) level = LogLevel.Fatal;
        else if (upper.Contains("[ERROR]") || upper.Contains("ERROR]")) level = LogLevel.Error;
        else if (upper.Contains("[WARN]") || upper.Contains("WARN]")) level = LogLevel.Warn;
        else if (upper.Contains("[DEBUG]") || upper.Contains("DEBUG]")) level = LogLevel.Debug;

        // Try to parse timestamp from "[HH:mm:ss] ..."
        var ts = DateTime.Now;
        if (raw.Length >= 10 && raw[0] == '[')
        {
            var close = raw.IndexOf(']');
            if (close > 0 && DateTime.TryParse(raw.AsSpan(1, close - 1), out var parsed))
                ts = DateTime.Today.Add(parsed.TimeOfDay);
        }

        return new LogEntry { Timestamp = ts, Level = level, Text = raw };
    }
}
