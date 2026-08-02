using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;
using YLproxy.Core.Config;
using YLproxy.Infrastructure;
using YLproxy.Models;

namespace YLproxy.Tests;

public class ProxyDataSerializerTests : IDisposable
{
    private readonly string _testConfigPath;

    public ProxyDataSerializerTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"ylproxy_serializer_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testConfigPath))
        {
            File.Delete(_testConfigPath);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_NullSecurityService_ShouldCreateDefault()
    {
        // Act
        var serializer = new ProxyDataSerializer(null);

        // Assert
        Assert.NotNull(serializer);
    }

    [Fact]
    public void Serialize_NullConfig_ShouldThrow()
    {
        // Arrange
        var serializer = new ProxyDataSerializer(null);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => serializer.Serialize(null!));
    }

    [Fact]
    public void Serialize_EmptyProxies_ShouldSucceed()
    {
        // Arrange
        var serializer = new ProxyDataSerializer(null);
        var config = new AppConfig
        {
            Version = "2.0",
            Proxies = new List<ProxyItem>()
        };

        // Act
        var json = serializer.Serialize(config);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("Version", json);
    }

    [Fact]
    public void Serialize_ValidConfig_ShouldIncludeVersion()
    {
        // Arrange
        var serializer = new ProxyDataSerializer(null);
        var config = new AppConfig
        {
            Version = "2.0",
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
        var json = serializer.Serialize(config);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("2.0", json);
        Assert.Contains("Test", json);
    }

    [Fact]
    public void Deserialize_NullJson_ShouldThrow()
    {
        // Arrange
        var serializer = new ProxyDataSerializer(null);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null!, out _));
    }

    [Fact]
    public void Deserialize_EmptyJson_ShouldThrow()
    {
        // Arrange
        var serializer = new ProxyDataSerializer(null);

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => serializer.Deserialize("", out _));
    }

    [Fact]
    public void Deserialize_InvalidJson_ShouldThrow()
    {
        // Arrange
        var serializer = new ProxyDataSerializer(null);

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => serializer.Deserialize("invalid json", out _));
    }

    [Fact]
    public void Deserialize_ValidJson_ShouldReturnConfig()
    {
        // Arrange
        var serializer = new ProxyDataSerializer(null);
        var json = @"{
            ""Version"": ""2.0"",
            ""Proxies"": []
        }";

        // Act
        var config = serializer.Deserialize(json, out var requiresMigration);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("2.0", config.Version);
        Assert.NotNull(config.Proxies);
        Assert.False(requiresMigration);
    }


    [Fact]
    public void SerializeDeserialize_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var serializer = new ProxyDataSerializer(null);
        var originalConfig = new AppConfig
        {
            Version = "2.0",
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
        var json = serializer.Serialize(originalConfig);
        var deserializedConfig = serializer.Deserialize(json, out var requiresMigration);

        // Assert
        Assert.NotNull(deserializedConfig);
        Assert.Equal(originalConfig.Version, deserializedConfig.Version);
        Assert.False(requiresMigration);
    }
}
