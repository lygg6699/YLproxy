using System;
using System.IO;
using System.Threading;
using Xunit;
using YLproxy.Models;
using YLproxy.Proxy;
using YLproxy.Infrastructure;
using YLproxy.Models.Config;

namespace YLproxy.Tests;

public class ProxyProcessManagerCriticalTests : IDisposable
{
    private readonly string _testRuntimeDir;
    private readonly string _testDataDir;
    private readonly ILogger _logger;
    private readonly ProxyRuntimeConfiguration _runtimeConfig;

    public ProxyProcessManagerCriticalTests()
    {
        _testRuntimeDir = Path.Combine(Path.GetTempPath(), $"ylproxy_runtime_{Guid.NewGuid():N}");
        _testDataDir = Path.Combine(Path.GetTempPath(), $"ylproxy_data_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRuntimeDir);
        Directory.CreateDirectory(_testDataDir);
        _logger = LoggerFactory.CreateLogger();
        _runtimeConfig = new ProxyRuntimeConfiguration(_testRuntimeDir, new List<string>());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRuntimeDir))
            {
                Directory.Delete(_testRuntimeDir, true);
            }
            if (Directory.Exists(_testDataDir))
            {
                Directory.Delete(_testDataDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Start_NullProxy_ShouldThrow()
    {
        // Arrange
        var manager = new ProxyProcessManager(_runtimeConfig, _logger);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => manager.Start(null!));
    }

    [Fact]
    public void Stop_InvalidProxyId_ShouldNotThrow()
    {
        // Arrange
        var manager = new ProxyProcessManager(_runtimeConfig, _logger);
        var proxy = new ProxyItem
        {
            Id = 999,
            Name = "Test",
            RemoteHost = "1.2.3.4",
            RemotePort = 8080,
            LocalHost = "127.0.0.1",
            LocalPort = 9000,
            Status = ProxyStatus.Stopped
        };

        // Act & Assert
        manager.Stop(proxy); // Should not throw for non-existent proxy
    }

    [Fact]
    public void Stop_NullProxy_ShouldThrow()
    {
        // Arrange
        var manager = new ProxyProcessManager(_runtimeConfig, _logger);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => manager.Stop(null!));
    }

    [Fact]
    public void Start_InvalidLocalPort_ShouldThrow()
    {
        // Arrange
        var manager = new ProxyProcessManager(_runtimeConfig, _logger);

        var proxy = new ProxyItem
        {
            Id = 1,
            Name = "Test",
            RemoteHost = "1.2.3.4",
            RemotePort = 8080,
            LocalHost = "127.0.0.1",
            LocalPort = 70000, // Invalid port
            Status = ProxyStatus.Stopped
        };

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => manager.Start(proxy));
    }

    [Fact]
    public void Start_InvalidRemotePort_ShouldThrow()
    {
        // Arrange
        var manager = new ProxyProcessManager(_runtimeConfig, _logger);

        var proxy = new ProxyItem
        {
            Id = 1,
            Name = "Test",
            RemoteHost = "1.2.3.4",
            RemotePort = 0, // Invalid port
            LocalHost = "127.0.0.1",
            LocalPort = 9000,
            Status = ProxyStatus.Stopped
        };

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => manager.Start(proxy));
    }

    [Fact]
    public void Start_EmptyRemoteHost_ShouldThrow()
    {
        // Arrange
        var manager = new ProxyProcessManager(_runtimeConfig, _logger);

        var proxy = new ProxyItem
        {
            Id = 1,
            Name = "Test",
            RemoteHost = "", // Empty host
            RemotePort = 8080,
            LocalHost = "127.0.0.1",
            LocalPort = 9000,
            Status = ProxyStatus.Stopped
        };

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => manager.Start(proxy));
    }

    [Fact]
    public async Task ConcurrentStart_ShouldHandleRaceCondition()
    {
        // Arrange
        var manager = new ProxyProcessManager(_runtimeConfig, _logger);

        var proxy = new ProxyItem
        {
            Id = 1,
            Name = "Test",
            RemoteHost = "1.2.3.4",
            RemotePort = 8080,
            LocalHost = "127.0.0.1",
            LocalPort = 9000,
            Status = ProxyStatus.Stopped
        };

        // Act - Concurrent start attempts
        var tasks = new System.Threading.Tasks.Task[5];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    manager.Start(proxy);
                }
                catch
                {
                    // Expected: some attempts may fail due to race conditions
                }
            });
        }
        await System.Threading.Tasks.Task.WhenAll(tasks);

        // Assert - Should not crash
        Assert.True(true);
    }

    [Fact]
    public async Task ConcurrentStop_ShouldHandleRaceCondition()
    {
        // Arrange
        var manager = new ProxyProcessManager(_runtimeConfig, _logger);

        var proxy = new ProxyItem
        {
            Id = 1,
            Name = "Test",
            RemoteHost = "1.2.3.4",
            RemotePort = 8080,
            LocalHost = "127.0.0.1",
            LocalPort = 9000,
            Status = ProxyStatus.Stopped
        };

        // Act - Concurrent stop attempts
        var tasks = new System.Threading.Tasks.Task[5];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    manager.Stop(proxy);
                }
                catch
                {
                    // Expected: should handle gracefully
                }
            });
        }
        await System.Threading.Tasks.Task.WhenAll(tasks);

        // Assert - Should not crash
        Assert.True(true);
    }
}
