using YLproxy.Infrastructure;

namespace YLproxy.Tests;

[Trait("Category", "Unit")]
public class PerformanceMonitorTests
{
    public PerformanceMonitorTests()
    {
        // Reset stats before each test
        PerformanceMonitor.Reset();
    }

    [Fact]
    public void MeasureOperation_ReturnsOperationTimer()
    {
        using var timer = PerformanceMonitor.MeasureOperation("test-op");
        Assert.NotNull(timer);
    }

    [Fact]
    public void MeasureOperation_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentException>(() => PerformanceMonitor.MeasureOperation(null!));
    }

    [Fact]
    public void RecordOperation_RecordsStats()
    {
        PerformanceMonitor.RecordOperation("test-op", TimeSpan.FromMilliseconds(100));

        var stats = PerformanceMonitor.GetStats("test-op");
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.Count);
        Assert.Equal(100, stats.TotalMilliseconds, 1);
    }

    [Fact]
    public void RecordOperation_MultipleCalls_AggregatesStats()
    {
        PerformanceMonitor.RecordOperation("test-op", TimeSpan.FromMilliseconds(100));
        PerformanceMonitor.RecordOperation("test-op", TimeSpan.FromMilliseconds(200));
        PerformanceMonitor.RecordOperation("test-op", TimeSpan.FromMilliseconds(300));

        var stats = PerformanceMonitor.GetStats("test-op");
        Assert.NotNull(stats);
        Assert.Equal(3, stats!.Count);
        Assert.Equal(600, stats.TotalMilliseconds, 1);
        Assert.Equal(100, stats.MinMilliseconds, 1);
        Assert.Equal(300, stats.MaxMilliseconds, 1);
    }

    [Fact]
    public void RecordOperation_TracksMinAndMax()
    {
        PerformanceMonitor.RecordOperation("range-op", TimeSpan.FromMilliseconds(50));
        PerformanceMonitor.RecordOperation("range-op", TimeSpan.FromMilliseconds(500));
        PerformanceMonitor.RecordOperation("range-op", TimeSpan.FromMilliseconds(10));

        var stats = PerformanceMonitor.GetStats("range-op");
        Assert.NotNull(stats);
        Assert.Equal(10, stats!.MinMilliseconds, 1);
        Assert.Equal(500, stats.MaxMilliseconds, 1);
    }

    [Fact]
    public void GetStats_ForUnknownOperation_ReturnsNull()
    {
        var stats = PerformanceMonitor.GetStats("unknown-op");
        Assert.Null(stats);
    }

    [Fact]
    public void GetAllStats_ReturnsSnapshot()
    {
        PerformanceMonitor.RecordOperation("op1", TimeSpan.FromMilliseconds(10));
        PerformanceMonitor.RecordOperation("op2", TimeSpan.FromMilliseconds(20));

        var allStats = PerformanceMonitor.GetAllStats();
        Assert.Equal(2, allStats.Count);
        Assert.Contains("op1", allStats.Keys);
        Assert.Contains("op2", allStats.Keys);
    }

    [Fact]
    public void Reset_ClearsAllStats()
    {
        PerformanceMonitor.RecordOperation("test", TimeSpan.FromMilliseconds(50));
        PerformanceMonitor.Reset();

        var stats = PerformanceMonitor.GetStats("test");
        Assert.Null(stats);
        Assert.Empty(PerformanceMonitor.GetAllStats());
    }

    [Fact]
    public void OperationTimer_Dispose_RecordsTiming()
    {
        // Use a fresh reset to isolate this test
        PerformanceMonitor.Reset();

        var opName = "timed-op-" + Guid.NewGuid().ToString("N")[..8];
        using (var timer = PerformanceMonitor.MeasureOperation(opName))
        {
            // Simulate some work
            Thread.Sleep(10);
        }

        var stats = PerformanceMonitor.GetStats(opName);
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.Count);
        Assert.True(stats.TotalMilliseconds >= 10, $"Expected >= 10ms, got {stats.TotalMilliseconds}");
    }

    [Fact]
    public void OperationTimer_MultipleInstances_RecordsSeparateTimings()
    {
        PerformanceMonitor.Reset();
        var opName = "multi-timed-op";

        using (var t1 = PerformanceMonitor.MeasureOperation(opName)) { Thread.Sleep(5); }
        using (var t2 = PerformanceMonitor.MeasureOperation(opName)) { Thread.Sleep(5); }

        var stats = PerformanceMonitor.GetStats(opName);
        Assert.NotNull(stats);
        Assert.Equal(2, stats!.Count);
    }

    [Fact]
    public void GetDiagnosticsReport_WithData_ReturnsFormattedString()
    {
        PerformanceMonitor.RecordOperation("report-op", TimeSpan.FromMilliseconds(100));

        var report = PerformanceMonitor.GetDiagnosticsReport();
        Assert.Contains("report-op", report);
        Assert.Contains("Performance Monitor Report", report);
        Assert.Contains("Count", report);
    }

    [Fact]
    public void GetDiagnosticsReport_WithoutData_ReturnsEmptyMessage()
    {
        PerformanceMonitor.Reset();
        var report = PerformanceMonitor.GetDiagnosticsReport();
        Assert.Contains("No performance data collected", report);
    }

    [Fact]
    public void OperationStats_AverageMilliseconds_IsCalculated()
    {
        PerformanceMonitor.RecordOperation("avg-op", TimeSpan.FromMilliseconds(100));
        PerformanceMonitor.RecordOperation("avg-op", TimeSpan.FromMilliseconds(200));

        var stats = PerformanceMonitor.GetStats("avg-op");
        Assert.NotNull(stats);
        Assert.Equal(150, stats!.AverageMilliseconds, 1);
    }

    [Fact]
    public void PerformanceMonitor_IsThreadSafe()
    {
        PerformanceMonitor.Reset();
        var opName = "threadsafe-op";
        var threads = new List<Thread>();

        for (int i = 0; i < 10; i++)
        {
            threads.Add(new Thread(() =>
            {
                using (PerformanceMonitor.MeasureOperation(opName))
                {
                    Thread.Sleep(1);
                }
            }));
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        var stats = PerformanceMonitor.GetStats(opName);
        Assert.NotNull(stats);
        Assert.Equal(10, stats!.Count);
        Assert.True(stats.AverageMilliseconds >= 1);
    }
}

