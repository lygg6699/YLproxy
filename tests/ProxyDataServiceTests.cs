using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using YLproxy.Core;
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
        Assert.Throws<ArgumentNullException>(() => new ProxyDataService(null!));
    }

    [Fact]
    public void Constructor_ValidPath_ShouldInitialize()
    {
        // Act
        var service = new ProxyDataService(_testConfigPath);

        // Assert
        Assert.NotNull(service);
        Assert.Equal(_testConfigPath, service.ConfigPath);
    }

    [Fact]
    public void Load_NonExistentFile_ShouldReturnEmptyConfig()
    {
        // Arrange
        var service = new ProxyDataService(_testConfigPath);

        // Act
        var config = service.Load();

        // Assert
        Assert.NotNull(config);
        Assert.NotNull(config.Proxies);
        Assert.Empty(config.Proxies);
    }

    [Fact]
    public void Save_NullConfig_ShouldThrow()
    {
        // Arrange
        var service = new ProxyDataService(_testConfigPath);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.Save(null!));
    }

    [Fact]
    public void Save_EmptyConfig_ShouldSucceed()
    {
        // Arrange
        var service = new ProxyDataService(_testConfigPath);
        var config = new AppConfig { Proxies = new List<ProxyItem>() };

        // Act
        service.Save(config);

        // Assert
        Assert.True(File.Exists(_testConfigPath));
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var service = new ProxyDataService(_testConfigPath);
        var originalConfig = new AppConfig
        {
            Version = "1.1",
            Proxies = new List<ProxyItem>
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
            }
        };

        // Act
        service.Save(originalConfig);
        var loadedConfig = service.Load();

        // Assert
        Assert.NotNull(loadedConfig);
        Assert.Single(loadedConfig.Proxies);
        Assert.Equal(originalConfig.Proxies[0].Name, loadedConfig.Proxies[0].Name);
        Assert.Equal(originalConfig.Proxies[0].RemoteHost, loadedConfig.Proxies[0].RemoteHost);
        Assert.Equal(originalConfig.Proxies[0].RemotePort, loadedConfig.Proxies[0].RemotePort);
    }

    [Fact]
    public void Save_MultipleProxies_ShouldPreserveAll()
    {
        // Arrange
        var service = new ProxyDataService(_testConfigPath);
        var config = new AppConfig
        {
            Version = "1.1",
            Proxies = new List<ProxyItem>
            {
                new ProxyItem { Id = 1, Name = "Test1", RemoteHost = "1.2.3.4", RemotePort = 8080, LocalPort = 9000 },
                new ProxyItem { Id = 2, Name = "Test2", RemoteHost = "5.6.7.8", RemotePort = 8080, LocalPort = 9001 },
                new ProxyItem { Id = 3, Name = "Test3", RemoteHost = "9.10.11.12", RemotePort = 8080, LocalPort = 9002 }
            }
        };

        // Act
        service.Save(config);
        var loadedConfig = service.Load();

        // Assert
        Assert.NotNull(loadedConfig);
        Assert.Equal(3, loadedConfig.Proxies.Count);
    }

    [Fact]
    public void Save_RepeatedWrites_ShouldRemainConsistent()
    {
        // Arrange
        var service = new ProxyDataService(_testConfigPath);

        // Act
        for (int i = 1; i <= 5; i++)
        {
            var config = new AppConfig
            {
                Version = "1.1",
                Proxies = new List<ProxyItem>
                {
                    new ProxyItem { Id = i, Name = $"Proxy{i}", RemoteHost = "1.2.3.4", RemotePort = 8080, LocalPort = 9000 + i }
                }
            };
            service.Save(config);
        }

        var loaded = service.Load();

        // Assert
        Assert.NotNull(loaded);
        Assert.Single(loaded.Proxies);
        Assert.Equal(5, loaded.Proxies[0].Id);
    }
}