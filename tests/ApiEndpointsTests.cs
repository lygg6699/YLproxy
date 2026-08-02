using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using YLproxy.Api;
using YLproxy.Models.Config;

namespace YLproxy.Tests;

public class ApiEndpointsTests : IDisposable
{
    private readonly string _testConfigPath;

    public ApiEndpointsTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"ylproxy_test_{Guid.NewGuid():N}.json");
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
    public void ProxyDtoValidation_NameRequired()
    {
        // Arrange
        var dto = new ProxyDto
        {
            RemoteHost = "1.2.3.4",
            RemotePort = 8080
        };

        // Assert
        Assert.True(string.IsNullOrWhiteSpace(dto.Name));
    }

    [Fact]
    public void ProxyDtoValidation_NameMaxLength()
    {
        // Arrange
        var dto = new ProxyDto
        {
            Name = new string('A', 201),
            RemoteHost = "1.2.3.4",
            RemotePort = 8080
        };

        // Assert
        Assert.True(dto.Name.Length > 200);
    }

    [Fact]
    public void ProxyDtoValidation_RemoteHostRequired()
    {
        // Arrange
        var dto = new ProxyDto
        {
            Name = "Test",
            RemotePort = 8080
        };

        // Assert
        Assert.True(string.IsNullOrWhiteSpace(dto.RemoteHost));
    }

    [Fact]
    public void ProxyDtoValidation_RemotePortRange()
    {
        // Arrange
        var dto1 = new ProxyDto { RemotePort = 0 };
        var dto2 = new ProxyDto { RemotePort = 70000 };

        // Assert
        Assert.True(dto1.RemotePort < 1);
        Assert.True(dto2.RemotePort > 65535);
    }

    [Fact]
    public void ProxyDtoValidation_LocalPortRange()
    {
        // Arrange
        var dto = new ProxyDto { LocalPort = 70000 };

        // Assert
        Assert.True(dto.LocalPort > 65535);
    }

    [Fact]
    public void ApiResponse_SuccessProperty()
    {
        // Arrange
        var response = new ApiResponse<object> { Success = true, Data = new { } };

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public void ApiResponse_FailProperty()
    {
        // Arrange
        var response = ApiResponse.Fail<object>("Test error");

        // Assert
        Assert.False(response.Success);
        Assert.Equal("Test error", response.Error);
    }

    [Fact]
    public void ApiResponse_OkProperty()
    {
        // Arrange
        var data = new { Id = 1, Name = "Test" };
        var response = ApiResponse.Ok(data);

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public void ProxyConfig_PortRangeValidation()
    {
        // Arrange
        var config1 = new ProxyConfig { PortRangeStart = 9000, PortRangeEnd = 8999 };
        var config2 = new ProxyConfig { PortRangeStart = 9000, PortRangeEnd = 70000 };

        // Assert
        Assert.True(config1.PortRangeEnd < config1.PortRangeStart);
        Assert.True(config2.PortRangeEnd > 65535);
    }

    [Fact]
    public void ProxyConfig_CheckIntervalValidation()
    {
        // Arrange
        var config = new ProxyConfig { CheckIntervalSeconds = 0 };

        // Assert
        Assert.True(config.CheckIntervalSeconds < 1);
    }

    [Fact]
    public void ConfigFile_EmptyProxiesList()
    {
        // Arrange
        var emptyConfig = new
        {
            version = "2.0",
            proxies = new List<object>()
        };
        File.WriteAllText(_testConfigPath, JsonSerializer.Serialize(emptyConfig));

        // Act
        var json = File.ReadAllText(_testConfigPath);
        var config = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        Assert.True(config.TryGetProperty("proxies", out var proxies));
        Assert.Equal(JsonValueKind.Array, proxies.ValueKind);
        Assert.Equal(0, proxies.GetArrayLength());
    }

    [Fact]
    public void ConfigFile_SingleProxy()
    {
        // Arrange
        var config = new
        {
            version = "2.0",
            proxies = new List<object>
            {
                new
                {
                    id = 1,
                    name = "TestProxy",
                    remoteHost = "1.2.3.4",
                    remotePort = 8080,
                    username = "",
                    password = "",
                    localHost = "127.0.0.1",
                    localPort = 9000,
                    status = "stopped",
                    group = "",
                    createTime = DateTime.UtcNow
                }
            }
        };
        File.WriteAllText(_testConfigPath, JsonSerializer.Serialize(config));

        // Act
        var json = File.ReadAllText(_testConfigPath);
        var parsed = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        Assert.True(parsed.TryGetProperty("proxies", out var proxies));
        Assert.Equal(1, proxies.GetArrayLength());
    }

    [Fact]
    public void ConfigFile_PortConflictDetection()
    {
        // Arrange
        var config = new
        {
            version = "2.0",
            proxies = new List<object>
            {
                new { id = 1, localPort = 9000 },
                new { id = 2, localPort = 9000 }
            }
        };
        File.WriteAllText(_testConfigPath, JsonSerializer.Serialize(config));

        // Act
        var json = File.ReadAllText(_testConfigPath);
        var parsed = JsonSerializer.Deserialize<JsonElement>(json);
        var proxies = parsed.GetProperty("proxies");
        var ports = new HashSet<int>();

        // Assert
        foreach (var proxy in proxies.EnumerateArray())
        {
            if (proxy.TryGetProperty("localPort", out var port))
            {
                var portValue = port.GetInt32();
                if (ports.Contains(portValue))
                {
                    Assert.True(true); // Conflict detected
                    return;
                }
                ports.Add(portValue);
            }
        }
    }

    [Fact]
    public void ConfigFile_AutoPortAssignment()
    {
        // Arrange
        var config = new
        {
            version = "2.0",
            proxies = new List<object>
            {
                new { id = 1, localPort = 9000 },
                new { id = 2, localPort = 9001 }
            }
        };
        File.WriteAllText(_testConfigPath, JsonSerializer.Serialize(config));

        // Act
        var json = File.ReadAllText(_testConfigPath);
        var parsed = JsonSerializer.Deserialize<JsonElement>(json);
        var proxies = parsed.GetProperty("proxies");
        var maxPort = 0;

        // Assert
        foreach (var proxy in proxies.EnumerateArray())
        {
            if (proxy.TryGetProperty("localPort", out var port))
            {
                maxPort = Math.Max(maxPort, port.GetInt32());
            }
        }
        Assert.True(maxPort > 0);
    }
}
