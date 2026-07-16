using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using YLproxy.Core;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Proxy;

namespace YLproxy.Tests;

[Trait("Category", "Unit")]
public sealed class LoggingAndMonitorTests
{
    [Fact]
    public void FileLoggerShouldRemoveExpiredLogFilesAndKeepRecentFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"YLproxy-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var currentLog = Path.Combine(directory, "log_current.txt");
            var recentLog = Path.Combine(directory, "log_recent.txt");
            var expiredLog = Path.Combine(directory, "log_expired.txt");
            var unrelatedFile = Path.Combine(directory, "notes.json");
            File.WriteAllText(currentLog, "current");
            File.WriteAllText(recentLog, "recent");
            File.WriteAllText(expiredLog, "expired");
            File.WriteAllText(unrelatedFile, "keep");

            File.SetLastWriteTimeUtc(currentLog, DateTime.UtcNow);
            File.SetLastWriteTimeUtc(recentLog, DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(expiredLog, DateTime.UtcNow.AddDays(-40));

            var logger = new FileLogger(directory, retentionDays: 30, minLevel: "Info");
            logger.CleanupOldLogs();

            Assert.True(File.Exists(currentLog));
            Assert.True(File.Exists(recentLog));
            Assert.False(File.Exists(expiredLog));
            Assert.True(File.Exists(unrelatedFile));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MonitorServiceShouldContinueAfterProcessInspectionFailures()
    {
        var proxy = new ProxyItem
        {
            Id = 501,
            Name = "Monitor test",
            RemoteHost = "127.0.0.1",
            RemotePort = 8080,
            LocalHost = "127.0.0.1",
            LocalPort = 9501,
            Status = ProxyStatus.Running,
        };
        var logs = new ConcurrentBag<string>();
        var logger = new TestLogger();
        var inspectionCount = 0;
        using var monitor = new MonitorService(
            getProxies: () => new[] { proxy },
            logAction: logs.Add,
            refreshAction: () => { },
            checkInterval: TimeSpan.FromMilliseconds(20),
            logger: logger,
            isRunning: _ =>
            {
                inspectionCount++;
                throw new Win32Exception("simulated access denied");
            });

        await Task.Delay(150);

        Assert.True(inspectionCount >= 2);
        Assert.Contains(logger.Warnings, message => message.Contains("proxy 501", StringComparison.Ordinal));
        Assert.Equal(ProxyStatus.Running, proxy.Status);
    }

    [Fact]
    public void ProxyProcessManagerStopShouldIgnoreMissingProcess()
    {
        var proxy = new ProxyItem
        {
            Id = 502,
            LocalHost = "127.0.0.1",
            LocalPort = 9502,
        };

        using var upstream = new TcpListener(IPAddress.Loopback, 0);
        upstream.Start();
        var upstreamPort = ((IPEndPoint)upstream.LocalEndpoint).Port;
        var forwarder = new TransparentCoalescingForwarder("127.0.0.1", upstreamPort, null, null);
        ProxyProcessManager.AddForwarderForTesting(proxy.Id, forwarder);

        var exception = Record.Exception(() => YLproxy.Proxy.ProxyProcessManager.Stop(proxy));

        Assert.Null(exception);
        Assert.False(ProxyProcessManager.HasActiveForwarderForTesting(proxy.Id));
    }

    private sealed class TestLogger : ILogger
    {
        public ConcurrentBag<string> Warnings { get; } = new();

        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) { }
        public void Fatal(string message) { }
        public void Debug(string message, Exception exception) { }
        public void Info(string message, Exception exception) { }
        public void Warn(string message, Exception exception) => Warnings.Add(message);
        public void Error(string message, Exception exception) { }
        public void Fatal(string message, Exception exception) { }
    }
}
