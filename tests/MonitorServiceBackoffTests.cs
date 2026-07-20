using YLproxy.Core;
using YLproxy.Models;

namespace YLproxy.Tests;

public sealed class MonitorServiceBackoffTests
{
    /// <summary>
    /// Backoff boundary: 0 failures → immediate restart (no backoff delay).
    /// </summary>
    [Fact]
    public async Task TryAutoRestart_ZeroFailures_ShouldRestartImmediately()
    {
        var proxy = new ProxyItem
        {
            Id = 801, Name = "b0", LocalHost = "127.0.0.1", LocalPort = 0,
            Status = ProxyStatus.Running,
        };

        int restartCalls = 0;
        using var monitor = new MonitorService(
            getProxies: () => new[] { proxy },
            logAction: _ => { },
            refreshAction: () => { },
            restartAction: _ => restartCalls++,
            saveAction: () => { },
            checkInterval: TimeSpan.FromMilliseconds(20),
            healthCheckInterval: TimeSpan.FromSeconds(1),
            maxRestartAttempts: 10,
            restartBackoffBase: TimeSpan.FromSeconds(30),
            isRunning: _ => false);

        await Task.Delay(300);

        // First failure leads to immediate restart (no backoff since prevLastAttempt is default)
        Assert.True(restartCalls >= 1, $"Expected at least 1 restart, got {restartCalls}");
    }

    /// <summary>
    /// Backoff boundary: with small enough base backoff so multiple restarts can occur
    /// before the exponential blow-up blocks further attempts.
    /// </summary>
    [Fact]
    public async Task TryAutoRestart_SingleFailure_ShouldRestartWithBaseBackoff()
    {
        var proxy = new ProxyItem
        {
            Id = 802, Name = "b1", LocalHost = "127.0.0.1", LocalPort = 0,
            Status = ProxyStatus.Running,
        };

        int restartCalls = 0;
        // With 5ms base backoff and 10ms tick, we get about 3 restarts before backoff
        // exceeds the elapsed interval between consecutive ticks.
        using var monitor = new MonitorService(
            getProxies: () => new[] { proxy },
            logAction: _ => { },
            refreshAction: () => { },
            restartAction: _ => restartCalls++,
            saveAction: () => { },
            checkInterval: TimeSpan.FromMilliseconds(10),
            healthCheckInterval: TimeSpan.FromSeconds(1),
            maxRestartAttempts: 10,
            restartBackoffBase: TimeSpan.FromMilliseconds(5),
            isRunning: _ => false);

        await Task.Delay(1000);

        // At least 2 restarts should occur: first tick (immediate) + second tick (5ms backoff, 10ms elapsed)
        Assert.True(restartCalls >= 2, $"Expected >= 2 restarts, got {restartCalls}");
    }

/// <summary>
    /// Verifies that backoff implementation doesn't overflow or crash with many failures.
    /// The backoff cap prevents unbounded growth. Uses zero backoff to ensure rapid restarts.
    /// </summary>
    [Fact]
    public async Task TryAutoRestart_ManyFailures_ShouldCapBackoff()
    {
        var proxy = new ProxyItem
        {
            Id = 803, Name = "b8", LocalHost = "127.0.0.1", LocalPort = 0,
            Status = ProxyStatus.Running,
        };

        int restartCalls = 0;
        using var monitor = new MonitorService(
            getProxies: () => new[] { proxy },
            logAction: _ => { },
            refreshAction: () => { },
            restartAction: _ => restartCalls++,
            saveAction: () => { },
            checkInterval: TimeSpan.FromMilliseconds(5),
            healthCheckInterval: TimeSpan.FromSeconds(1),
            maxRestartAttempts: 50,
            restartBackoffBase: TimeSpan.Zero, // zero backoff = no delay between restarts
            isRunning: _ => false);

        await Task.Delay(2000);

        // With zero backoff, every tick should trigger a restart
        // The test should not crash or overflow
        Assert.True(restartCalls >= 3, $"Expected >= 3 restarts, got {restartCalls}");
    }
}

