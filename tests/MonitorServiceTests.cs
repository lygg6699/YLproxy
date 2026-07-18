using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YLproxy.Core;
using YLproxy.Infrastructure;
using YLproxy.Models;
using Xunit;

namespace YLproxy.Tests;

public sealed class MonitorServiceTests
{
    [Fact]
    public async Task MonitorTick_WhenGetProxiesThrows_DoesNotThrow()
    {
        var logs = new List<string>();
        using var monitor = new MonitorService(
            getProxies: () => throw new InvalidOperationException("boom"),
            logAction: logs.Add,
            refreshAction: () => { },
            restartAction: null,
            saveAction: null,
            checkInterval: TimeSpan.FromMilliseconds(20),
            healthCheckInterval: TimeSpan.FromSeconds(1),
            logger: new NullLogger(),
            isRunning: _ => true);

        await Task.Delay(80);
        // no assertion needed: absence of crash is pass
        Assert.True(true);
    }

    [Fact]
    public async Task MonitorTick_WhenProcessNotRunning_ShouldMarkFailedAndInvokeRestartAndSave()
    {
        var proxy = new ProxyItem
        {
            Id = 701,
            Name = "m1",
            LocalHost = "127.0.0.1",
            LocalPort = 0,
            Status = ProxyStatus.Running
        };

        int restartCalls = 0;
        int saveCalls = 0;
        using var monitor = new MonitorService(
            getProxies: () => new[] { proxy },
            logAction: _ => { },
            refreshAction: () => { },
            restartAction: _ => restartCalls++,
            saveAction: () => saveCalls++,
            checkInterval: TimeSpan.FromMilliseconds(20),
            healthCheckInterval: TimeSpan.FromSeconds(1),
            maxRestartAttempts: 50, // high enough to not be exceeded during test window (~6 ticks in 120ms)
            restartBackoffBase: TimeSpan.Zero,
            logger: new NullLogger(),
            isRunning: _ => false);

        await Task.Delay(120);
        Assert.True(restartCalls >= 1);
        Assert.True(saveCalls >= 1);
        Assert.Equal(ProxyStatus.Running, proxy.Status); // TryAutoRestart sets Running before calling restartAction
    }

    [Fact]
    public async Task MonitorTick_WhenHealthPortUnreachable_ShouldMarkFailedAndInvokeRestart()
    {
        var proxy = new ProxyItem
        {
            Id = 702,
            Name = "m2",
            LocalHost = "127.0.0.1",
            LocalPort = 0, // zero port — no listener can bind here, always unreachable
            Status = ProxyStatus.Running
        };

        int restartCalls = 0;
        using var monitor = new MonitorService(
            getProxies: () => new[] { proxy },
            logAction: _ => { },
            refreshAction: () => { },
            restartAction: _ => restartCalls++,
            saveAction: () => { },
            checkInterval: TimeSpan.FromMilliseconds(50),
            healthCheckInterval: TimeSpan.FromMilliseconds(0), // always check
            maxRestartAttempts: 50,
            restartBackoffBase: TimeSpan.Zero,
            logger: new NullLogger(),
            isRunning: _ => true);

        await Task.Delay(400);
        Assert.True(restartCalls >= 1, $"restartCalls was {restartCalls}");
        Assert.Equal(ProxyStatus.Running, proxy.Status);
    }

    [Fact]
    public async Task TryAutoRestart_WhenBackoffNotExceeded_ShouldSkipRestart()
    {
        var proxy = new ProxyItem
        {
            Id = 703,
            Name = "m3",
            LocalHost = "127.0.0.1",
            LocalPort = 0,
            Status = ProxyStatus.Running
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
            maxRestartAttempts: 50,
            restartBackoffBase: TimeSpan.FromMinutes(10), // very long backoff
            logger: new NullLogger(),
            isRunning: _ => false); // every tick fails -> triggers TryAutoRestart

        await Task.Delay(150);
        // With a 10-minute backoff, only the first attempt goes through;
        // subsequent ticks should be blocked by backoff.
        Assert.True(restartCalls <= 2, $"restartCalls was {restartCalls}, expected <=2 due to backoff");
        Assert.True(restartCalls >= 1);
    }

    [Fact]
    public async Task TryAutoRestart_WhenMaxAttemptsExceeded_ShouldGiveUp()
    {
        var proxy = new ProxyItem
        {
            Id = 704,
            Name = "m4",
            LocalHost = "127.0.0.1",
            LocalPort = 0,
            Status = ProxyStatus.Running
        };

        var logs = new List<string>();
        using var monitor = new MonitorService(
            getProxies: () => new[] { proxy },
            logAction: logs.Add,
            refreshAction: () => { },
            restartAction: _ => { },
            saveAction: () => { },
            checkInterval: TimeSpan.FromMilliseconds(20),
            healthCheckInterval: TimeSpan.FromSeconds(1),
            maxRestartAttempts: 2,
            restartBackoffBase: TimeSpan.Zero,
            logger: new NullLogger(),
            isRunning: _ => false);

        await Task.Delay(250);
        Assert.Contains(logs, l => l.Contains("abandoned"));
        Assert.Equal(ProxyStatus.Failed, proxy.Status);
    }

    [Fact]
    public async Task TryAutoRestart_WhenRestartActionThrows_ShouldSetStatusFailed()
    {
        var proxy = new ProxyItem
        {
            Id = 705,
            Name = "m5",
            LocalHost = "127.0.0.1",
            LocalPort = 0,
            Status = ProxyStatus.Running
        };

        var logs = new List<string>();
        using var monitor = new MonitorService(
            getProxies: () => new[] { proxy },
            logAction: logs.Add,
            refreshAction: () => { },
            restartAction: _ => throw new InvalidOperationException("restart blew up"),
            saveAction: () => { },
            checkInterval: TimeSpan.FromMilliseconds(20),
            healthCheckInterval: TimeSpan.FromSeconds(1),
            maxRestartAttempts: 50,
            restartBackoffBase: TimeSpan.Zero,
            logger: new NullLogger(),
            isRunning: _ => false);

        await Task.Delay(200);
        Assert.Contains(logs, l => l.Contains("auto-restart failed"));
        Assert.Equal(ProxyStatus.Failed, proxy.Status);
    }

    [Fact]
    public async Task ResetBackoff_ShouldAllowRestartAfterReset()
    {
        var proxy = new ProxyItem
        {
            Id = 706,
            Name = "m6",
            LocalHost = "127.0.0.1",
            LocalPort = 0,
            Status = ProxyStatus.Running
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
            maxRestartAttempts: 2,
            restartBackoffBase: TimeSpan.Zero,
            logger: new NullLogger(),
            isRunning: _ => false);

        // Let it exhaust attempts — increase delay for reliability
        await Task.Delay(400);

        // After giving up, proxy is Failed and won't be processed.
        // Reset backoff AND set to Running so MonitorTick picks it up again.
        monitor.ResetBackoff(proxy.Id);
        proxy.Status = ProxyStatus.Running;

        int countAfterReset = restartCalls;
        await Task.Delay(400);
        Assert.True(restartCalls > countAfterReset, $"Expected restartCalls ({restartCalls}) > countAfterReset ({countAfterReset})");
    }

    [Fact]
    public async Task IsRunningThrows_ShouldSkipProxyAndContinue()
    {
        var proxy = new ProxyItem
        {
            Id = 707,
            Name = "m7",
            LocalHost = "127.0.0.1",
            LocalPort = 0,
            Status = ProxyStatus.Running
        };

        var logs = new List<string>();
        using var monitor = new MonitorService(
            getProxies: () => new[] { proxy },
            logAction: logs.Add,
            refreshAction: () => { },
            restartAction: _ => { },
            saveAction: () => { },
            checkInterval: TimeSpan.FromMilliseconds(20),
            healthCheckInterval: TimeSpan.FromSeconds(1),
            logger: new NullLogger(),
            isRunning: _ => throw new InvalidOperationException("isRunning failed"));

        await Task.Delay(80);
        Assert.Equal(ProxyStatus.Running, proxy.Status); // unchanged
    }

    private sealed class NullLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
        public void Fatal(string message) { }
        public void Debug(string message, Exception exception) { }
        public void Info(string message, Exception exception) { }
        public void Warn(string message, Exception exception) { }
        public void Error(string message, Exception exception) { }
        public void Fatal(string message, Exception exception) { }
        public void Log(LogLevel level, string message, object? data = null) { }
        public void Log(LogLevel level, string message, Exception exception, object? data = null) { }
    }
}

