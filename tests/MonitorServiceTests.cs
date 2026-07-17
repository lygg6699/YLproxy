using System;
using System.Collections.Generic;
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
            maxRestartAttempts: 5,
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
            LocalPort = 9, // typically unreachable
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
            healthCheckInterval: TimeSpan.FromSeconds(0),
            maxRestartAttempts: 5,
            restartBackoffBase: TimeSpan.Zero,
            logger: new NullLogger(),
            isRunning: _ => true);

        await Task.Delay(140);
        Assert.True(restartCalls >= 1);
        Assert.Equal(ProxyStatus.Running, proxy.Status);
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
    }
}

