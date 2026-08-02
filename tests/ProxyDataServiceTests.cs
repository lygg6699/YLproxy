using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;
using YLproxy.Core;
using YLproxy.Infrastructure;
using YLproxy.Models;

namespace YLproxy.Tests;

public class ProxyDataServiceTests : IDisposable
{
    private readonly string _testConfigPath;
    private readonly string _testDataDir;

    public ProxyDataServiceTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), $"ylproxy_data_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDataDir);
        _testConfigPath = Path.Combine(_testDataDir, "config.json");
    }

    public void Dispose()
    {
        try
        {
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
    public void Constructor_NullConfigPath_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ProxyDataService(null!, _testDataDir));
    }

    [Fact]
    public void Constructor_NullDataDir_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ProxyDataService(_testConfigPath, null!));
    }

    [Fact]
    public void Constructor_ValidPaths_ShouldInitialize()
    {
        // Act
        var service = new ProxyDataService(_testConfigPath, _testDataDir);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Load_NonExistentFile_ShouldReturnEmpty()
    {
        // Arrange
        var service = new ProxyDataService(_testConfigPath, _testDataDir);

        // Act
        var proxies = service.Load();

        // Assert
        Assert.NotNull(proxies);
        Assert.Empty(proxies);
    }

    [Fact]
    public void Save_NullProxies_ShouldThrow()
    {
        // Arrange
        var service = new ProxyDataService(_testConfigPath, _testDataDir);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.Save(null!));
    }

    [Fact]
    public void Save_EmptyProxies_ShouldSucceed()
    {
        // Arrange
        var service = new ProxyDataService(_testConfigPath, _testDataDir);
        var proxies = new List<ProxyItem>();

        // Act
        service.Save(proxies);

        // Assert
        Assert.True(File.Exists(_testConfigPath));
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var service = new ProxyDataService(_testConfigPath, _testDataDir);
        var originalProxies = new List<ProxyItem>
        {
            new ProxyItem
            {
                Id = 1,
                Name = "Test",
                RemoteHost = "1.2.3.4",
                RemotePort = 8080,
                LocalHost = "127.0.0.1",
                LocalPort = 9000,
                Status = ProxyStatus.Stopped
            }
        };

        // Act
        service.Save(originalProxies);
        var loadedProxies = service.Load();

        // Assert
        Assert.NotNull(loadedProxies);
        Assert.Single(loadedProxies);
        Assert.Equal(originalProxies[0].Name, loadedProxies[0].Name);
        Assert.Equal(originalProxies[0].RemoteHost, loadedProxies[0].RemoteHost);
    }

    [Fact]
    public void Save_MultipleProxies_ShouldPreserveAll()
    {
        // Arrange
        var service = new ProxyDataService(_testConfigPath, _testDataDir);
        var proxies = new List<ProxyItem>
        {
            new ProxyItem { Id = 1, Name = "Test1", RemoteHost = "1.2.3.4", RemotePort = 8080, LocalPort = 9000 },
            new ProxyItem { Id = 2, Name = "Test2", RemoteHost = "5.6.7.8", RemotePort = 8080, LocalPort = 9001 },
            new ProxyItem { Id = 3, Name = "Test3", RemoteHost = "9.10.11.12", RemotePort = 8080, LocalPort = 9002 }
        };

        // Act
        service.Save(proxies);
        var loadedProxies = service.Load();

        // Assert
        Assert.NotNull(loadedProxies);
        Assert.Equal(3, loadedProxies.Count);
    }
}
