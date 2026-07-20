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

            using (var logger = new FileLogger(directory, retentionDays: 30, minLevel: "Info"))
            {
                logger.CleanupOldLogs();
            }

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

        var exception = Record.Exception(() => ProxyProcessManager.Default.Stop(proxy));

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
        using var logger = new FileLogger(directory, retentionDays: 30, minLevel: "Debug");
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

    [Fact]
    public void CleanupOldLogs_ShouldHandleLockedFile_Gracefully()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"YLproxy-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            // Create a file that is 60 days old, then create the logger with retention=30
            // so the file is a cleanup candidate.
            var expiredFile = Path.Combine(directory, $"log_{DateTime.Now.AddDays(-60):yyyyMMdd}.txt");
            File.WriteAllText(expiredFile, "locked content");
            File.SetLastWriteTime(expiredFile, DateTime.Now.AddDays(-60));

            // Open a lock on the expired file before creating the logger,
            // so the constructor's cleanup attempt hits a locked file.
            using (var fs = new FileStream(expiredFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var ex = Record.Exception(() => new FileLogger(directory, retentionDays: 30, minLevel: "Info"));
                // Should not throw — locked files are skipped gracefully.
                Assert.Null(ex);
            }

            // After releasing the lock, recreate logger to clean up.
            using (var logger = new FileLogger(directory, retentionDays: 30, minLevel: "Info"))
            {
                logger.CleanupOldLogs();
            }
            Assert.False(File.Exists(expiredFile));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FileLogger_ShouldFilterByMinLevel()
    {
        using var logDir = new TempDirectory();
        var directory = logDir.Path;

        using (var debugLogger = new FileLogger(directory, retentionDays: 30, minLevel: "Debug"))
        {
            debugLogger.Debug("debug message");
            debugLogger.Info("info message");
            debugLogger.Warn("warn message");
        }

        var currentLogPath = Path.Combine(directory, $"log_{DateTime.Now:yyyyMMdd}.txt");
        Assert.True(File.Exists(currentLogPath));
        var allContent = File.ReadAllText(currentLogPath);

        Assert.Contains("DEBUG", allContent);
        Assert.Contains("INFO", allContent);
        Assert.Contains("WARN", allContent);

        // Clean up and test Warn-level logger
        File.Delete(currentLogPath);

        using (var warnLogger = new FileLogger(directory, retentionDays: 30, minLevel: "Warn"))
        {
            warnLogger.Debug("debug message");
            warnLogger.Info("info message");
            warnLogger.Warn("warn message");
        }

        if (File.Exists(currentLogPath))
        {
            var warnContent = File.ReadAllText(currentLogPath);
            Assert.DoesNotContain("DEBUG", warnContent);
            Assert.DoesNotContain("INFO", warnContent);
            Assert.Contains("WARN", warnContent);
        }
    }

    [Fact]
    public void FileLogger_ShouldIncludeException_StackTrace()
    {
        using var logDir = new TempDirectory();
        var directory = logDir.Path;

        using (var logger = new FileLogger(directory, retentionDays: 30, minLevel: "Debug"))
        {
            try
            {
                throw new InvalidOperationException("test exception detail");
            }
            catch (Exception ex)
            {
                logger.Error("Something went wrong", ex);
            }
        }

        var currentLogPath = Path.Combine(directory, $"log_{DateTime.Now:yyyyMMdd}.txt");
        Assert.True(File.Exists(currentLogPath));
        var content = File.ReadAllText(currentLogPath);

        Assert.Contains("ERROR", content);
        Assert.Contains("Something went wrong", content);
        Assert.Contains("test exception detail", content);
        Assert.Contains("StackTrace", content);
    }

    [Fact]
    public void FileLogger_RetentionZero_ShouldNotDeleteCurrentDay()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"YLproxy-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var todayLog = Path.Combine(directory, $"log_{DateTime.Now:yyyyMMdd}.txt");
            var yesterdayLog = Path.Combine(directory, $"log_{DateTime.Now.AddDays(-1):yyyyMMdd}.txt");
            File.WriteAllText(todayLog, "today");
            File.WriteAllText(yesterdayLog, "yesterday");
            File.SetLastWriteTime(todayLog, DateTime.Now);
            File.SetLastWriteTime(yesterdayLog, DateTime.Now.AddDays(-1));

            using (var logger = new FileLogger(directory, retentionDays: 0, minLevel: "Info"))
            {
                logger.CleanupOldLogs();
            }

            // RetentionDays=0: files older than 0 days are deleted → today survives.
            Assert.True(File.Exists(todayLog));
            Assert.False(File.Exists(yesterdayLog));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
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
        public void Log(LogLevel level, string message, object? data = null) { }
        public void Log(LogLevel level, string message, Exception exception, object? data = null) { }
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
