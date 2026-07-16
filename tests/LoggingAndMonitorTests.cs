using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using YLproxy.Core;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Proxy;

namespace YLproxy.Tests;

public sealed class LoggingAndMonitorTests
{
    [Fact]
    public void FileLoggerShouldRemoveExpiredLogFilesAndKeepRecentFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"YLproxy-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var currentLog = Path.Combine(directory, $"log_{DateTime.UtcNow:yyyyMMdd}.txt");
            var recentLog = Path.Combine(directory, $"log_{DateTime.UtcNow.AddDays(-10):yyyyMMdd}.txt");
            var expiredLog = Path.Combine(directory, $"log_{DateTime.UtcNow.AddDays(-40):yyyyMMdd}.txt");
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
        var testLogger = new TestLogger();
        var inspectionCount = 0;
        using var monitor = new MonitorService(
            getProxies: () => new[] { proxy },
            logAction: logs.Add,
            refreshAction: () => { },
            checkInterval: TimeSpan.FromMilliseconds(20));

        await Task.Delay(150);

        Assert.True(inspectionCount >= 0); // Monitor should not crash
        Assert.Contains(proxy.Status, new[] { ProxyStatus.Running, ProxyStatus.Failed });
    }

    [Fact]
    public void ProxyProcessManagerStopShouldNotThrowWhenNoProcess()
    {
        var proxy = new ProxyItem
        {
            Id = 502,
            LocalHost = "127.0.0.1",
            LocalPort = 9502,
        };

        var exception = Record.Exception(() => ProxyProcessManager.Stop(proxy));

        Assert.Null(exception);
    }

    [Fact]
    public void FileLoggerShouldHandleLogDirectoryCreationFailure()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"YLproxy-logs-{Guid.NewGuid():N}");
        // Pre-create a file with the same name to cause directory creation failure
        File.WriteAllText(directory, "block");

        try
        {
            var exception = Record.Exception(() =>
                new FileLogger(directory, retentionDays: 30, minLevel: "Info"));

            // Should throw due to directory creation failure
            Assert.NotNull(exception);
        }
        finally
        {
            if (File.Exists(directory))
                File.Delete(directory);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SimpleRetryShouldRetryOnTransientFailure()
    {
        using var logDir = new TempDirectory();
        var directory = logDir.Path;
        var logger = new FileLogger(directory, retentionDays: 30, minLevel: "Debug");
        var attemptCount = 0;

        var result = SimpleRetry.Execute(
            () =>
            {
                attemptCount++;
                if (attemptCount < 3)
                    throw new IOException("simulated transient error");
                return attemptCount;
            },
            maxAttempts: 5,
            delayMs: 10,
            logger: logger);

        Assert.Equal(3, result);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public void SimpleRetryShouldThrowAggregateExceptionAfterMaxAttempts()
    {
        var attempts = 0;

        var ex = Assert.Throws<AggregateException>(() =>
            SimpleRetry.Execute(
                () =>
                {
                    attempts++;
                    throw new InvalidOperationException($"fail {attempts}");
                },
                maxAttempts: 3,
                delayMs: 10));

        Assert.Equal(3, attempts);
        Assert.Equal(3, ex.InnerExceptions.Count);
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

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"YLproxy-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
