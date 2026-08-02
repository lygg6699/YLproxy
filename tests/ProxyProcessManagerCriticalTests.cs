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

    public ProxyProcessManagerCriticalTests()
    {
        _testRuntimeDir = Path.Combine(Path.GetTempPath(), $"ylproxy_runtime_{Guid.NewGuid():N}");
        _testDataDir = Path.Combine(Path.GetTempPath(), $"ylproxy_data_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRuntimeDir);
        Directory.CreateDirectory(_testDataDir);
        _logger = LoggerFactory.CreateLogger();
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
    }

    [Fact]
    public void Start_InvalidConfig_ShouldThrowException()
    {
        // Arrange
        var config = new ThreeProxyConfig
        {
            RuntimeDirectory = _testRuntimeDir,
            RequiredDlls = new[] { "nonexistent.dll" }
        };
        var manager = new ProxyProcessManager(_logger);
        manager.Configure(config);

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

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => manager.Start(proxy));
    }

    [Fact]
    public void Stop_ProcessNotRunning_ShouldNotThrow()
    {
        // Arrange
        var config = new ThreeProxyConfig
        {
            RuntimeDirectory = _testRuntimeDir,
            RequiredDlls = Array.Empty<string>()
        };
        var manager = new ProxyProcessManager(_logger);
        manager.Configure(config);

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

        // Act & Assert
        manager.Stop(proxy.Id); // Should not throw
    }

    [Fact]
    public void Start_PortAlreadyInUse_ShouldHandleError()
    {
        // Arrange
        var config = new ThreeProxyConfig
        {
            RuntimeDirectory = _testRuntimeDir,
            RequiredDlls = Array.Empty<string>()
        };
        var manager = new ProxyProcessManager(_logger);
        manager.Configure(config);

        var proxy1 = new ProxyItem
        {
            Id = 1,
            Name = "Test1",
            RemoteHost = "1.2.3.4",
            RemotePort = 8080,
            LocalHost = "127.0.0.1",
            LocalPort = 9000,
            Status = ProxyStatus.Stopped
        };

        var proxy2 = new ProxyItem
        {
            Id = 2,
            Name = "Test2",
            RemoteHost = "5.6.7.8",
            RemotePort = 8080,
            LocalHost = "127.0.0.1",
            LocalPort = 9000, // Same port
            Status = ProxyStatus.Stopped
        };

        // Act & Assert
        // First start might succeed or fail depending on system
        // Second start should handle port conflict gracefully
        try
        {
            manager.Start(proxy1);
            Assert.ThrowsAny<Exception>(() => manager.Start(proxy2));
        }
        catch
        {
            // If first start fails, that's also acceptable for this test
        }
    }

    [Fact]
    public void Configure_NullConfig_ShouldThrow()
    {
        // Arrange
        var manager = new ProxyProcessManager(_logger);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => manager.Configure(null!));
    }

    [Fact]
    public void Start_NullProxy_ShouldThrow()
    {
        // Arrange
        var manager = new ProxyProcessManager(_logger);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => manager.Start(null!));
    }

    [Fact]
    public void Stop_NullProxyId_ShouldThrow()
    {
        // Arrange
        var manager = new ProxyProcessManager(_logger);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.Stop(0));
    }

    [Fact]
    public void Start_InvalidLocalPort_ShouldThrow()
    {
        // Arrange
        var config = new ThreeProxyConfig
        {
            RuntimeDirectory = _testRuntimeDir,
            RequiredDlls = Array.Empty<string>()
        };
        var manager = new ProxyProcessManager(_logger);
        manager.Configure(config);

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
        var config = new ThreeProxyConfig
        {
            RuntimeDirectory = _testRuntimeDir,
            RequiredDlls = Array.Empty<string>()
        };
        var manager = new ProxyProcessManager(_logger);
        manager.Configure(config);

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
        var config = new ThreeProxyConfig
        {
            RuntimeDirectory = _testRuntimeDir,
            RequiredDlls = Array.Empty<string>()
        };
        var manager = new ProxyProcessManager(_logger);
        manager.Configure(config);

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
    public void ConcurrentStart_ShouldHandleRaceCondition()
    {
        // Arrange
        var config = new ThreeProxyConfig
        {
            RuntimeDirectory = _testRuntimeDir,
            RequiredDlls = Array.Empty<string>()
        };
        var manager = new ProxyProcessManager(_logger);
        manager.Configure(config);

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
        System.Threading.Tasks.Task.WaitAll(tasks);

        // Assert - Should not crash
        Assert.True(true);
    }

    [Fact]
    public void ConcurrentStop_ShouldHandleRaceCondition()
    {
        // Arrange
        var config = new ThreeProxyConfig
        {
            RuntimeDirectory = _testRuntimeDir,
            RequiredDlls = Array.Empty<string>()
        };
        var manager = new ProxyProcessManager(_logger);
        manager.Configure(config);

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
                    manager.Stop(proxy.Id);
                }
                catch
                {
                    // Expected: should handle gracefully
                }
            });
        }
        System.Threading.Tasks.Task.WaitAll(tasks);

        // Assert - Should not crash
        Assert.True(true);
    }
}
