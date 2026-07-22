using System.Collections.Concurrent;
using System.Diagnostics;

namespace YLproxy.Infrastructure;

/// <summary>
/// Provides lightweight performance monitoring with operation timing and
/// diagnostic metrics collection.
/// </summary>
public static class PerformanceMonitor
{
    private static readonly ConcurrentDictionary<string, OperationStats> _stats = new(StringComparer.OrdinalIgnoreCase);
    private static ILogger? _logger;

    /// <summary>
    /// Sets the logger for performance warnings and diagnostics.
    /// </summary>
    public static void SetLogger(ILogger logger) => _logger = logger;

    /// <summary>
    /// Starts measuring an operation. Returns an <see cref="IDisposable"/> that
    /// records the elapsed time when disposed (typically via a `using` statement).
    /// </summary>
    /// <param name="operationName">Name of the operation being measured.</param>
    /// <param name="thresholdMs">Optional threshold in milliseconds. If the operation
    /// exceeds this threshold, a warning is logged.</param>
    /// <returns>An <see cref="OperationTimer"/> that records timing upon disposal.</returns>
    public static OperationTimer MeasureOperation(string operationName, long thresholdMs = -1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        return new OperationTimer(operationName, thresholdMs, _logger);
    }

    /// <summary>
    /// Records a completed operation's elapsed time.
    /// </summary>
    public static void RecordOperation(string operationName, TimeSpan elapsed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        _stats.AddOrUpdate(operationName,
            _ => new OperationStats
            {
                Count = 1,
                TotalMilliseconds = elapsed.TotalMilliseconds,
                MinMilliseconds = elapsed.TotalMilliseconds,
                MaxMilliseconds = elapsed.TotalMilliseconds
            },
            (_, existing) =>
            {
                existing.Count++;
                existing.TotalMilliseconds += elapsed.TotalMilliseconds;
                if (elapsed.TotalMilliseconds < existing.MinMilliseconds)
                    existing.MinMilliseconds = elapsed.TotalMilliseconds;
                if (elapsed.TotalMilliseconds > existing.MaxMilliseconds)
                    existing.MaxMilliseconds = elapsed.TotalMilliseconds;
                return existing;
            });
    }

    /// <summary>
    /// Gets the current statistics for a given operation.
    /// </summary>
    public static OperationStats? GetStats(string operationName)
    {
        _stats.TryGetValue(operationName, out var stats);
        return stats;
    }

    /// <summary>
    /// Gets a snapshot of all recorded operation statistics.
    /// </summary>
    public static IReadOnlyDictionary<string, OperationStats> GetAllStats()
    {
        return new Dictionary<string, OperationStats>(_stats, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resets all collected statistics.
    /// </summary>
    public static void Reset()
    {
        _stats.Clear();
    }

    /// <summary>
    /// Formats the current statistics as a diagnostic string.
    /// </summary>
    public static string GetDiagnosticsReport()
    {
        if (_stats.IsEmpty)
            return "No performance data collected.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Performance Monitor Report ===");
        sb.AppendLine($"{"Operation",-30} {"Count",8} {"Avg (ms)",12} {"Min (ms)",12} {"Max (ms)",12}");
        sb.AppendLine(new string('-', 74));

        foreach (var kvp in _stats.OrderByDescending(s => s.Value.TotalMilliseconds))
        {
            var s = kvp.Value;
            var avg = s.Count > 0 ? s.TotalMilliseconds / s.Count : 0;
            sb.AppendLine($"{kvp.Key,-30} {s.Count,8} {avg,12:F2} {s.MinMilliseconds,12:F2} {s.MaxMilliseconds,12:F2}");
        }

        sb.AppendLine(new string('-', 74));
        return sb.ToString();
    }

    /// <summary>
    /// Tracks timing for a single operation invocation.
    /// </summary>
    public sealed class OperationTimer : IDisposable
    {
        private readonly string _operationName;
        private readonly long _thresholdMs;
        private readonly ILogger? _logger;
        private readonly Stopwatch _stopwatch;
        private int _disposed;

        internal OperationTimer(string operationName, long thresholdMs, ILogger? logger)
        {
            _operationName = operationName;
            _thresholdMs = thresholdMs;
            _logger = logger;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _stopwatch.Stop();
            var elapsed = _stopwatch.Elapsed;

            RecordOperation(_operationName, elapsed);

            if (_thresholdMs > 0 && elapsed.TotalMilliseconds > _thresholdMs)
            {
                _logger?.Warn($"Performance threshold exceeded: '{_operationName}' took {elapsed.TotalMilliseconds:F2}ms (threshold: {_thresholdMs}ms)");
            }
        }
    }

    /// <summary>
    /// Aggregated statistics for a named operation.
    /// </summary>
    public sealed class OperationStats
    {
        /// <summary>Number of times the operation has been recorded.</summary>
        public long Count { get; set; }

        /// <summary>Total elapsed time in milliseconds.</summary>
        public double TotalMilliseconds { get; set; }

        /// <summary>Minimum recorded elapsed time in milliseconds.</summary>
        public double MinMilliseconds { get; set; }

        /// <summary>Maximum recorded elapsed time in milliseconds.</summary>
        public double MaxMilliseconds { get; set; }

        /// <summary>Average elapsed time in milliseconds.</summary>
        public double AverageMilliseconds => Count > 0 ? TotalMilliseconds / Count : 0;
    }
}

