using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using YLproxy.Api;
using YLproxy.Models.Config;

namespace YLproxy.Tests;

public class ApiEndpointsTests : IDisposable
{
    private readonly IHost _host;
    private readonly HttpClient _client;
    private readonly string _testConfigPath;

    public ApiEndpointsTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"ylproxy_test_{Guid.NewGuid():N}.json");

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.Configure(app =>
                {
                    var proxyConfig = new ProxyConfig
                    {
                        PortRangeStart = 9000,
                        PortRangeEnd = 9999
                    };
                    ApiEndpoints.Map(app, _testConfigPath, proxyConfig);
                });
            });

        _host = hostBuilder.Start();
        _client = _host.GetTestClient();
    }

    public void Dispose()
    {
        _client?.Dispose();
        _host?.Dispose();

        if (File.Exists(_testConfigPath))
        {
            File.Delete(_testConfigPath);
        }
    }

    [Fact]
    public async Task Health_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("ok", content);
    }

    [Fact]
    public async Task GetProxies_EmptyConfig_ShouldReturnEmptyList()
    {
        // Arrange
        EnsureEmptyConfig();

        // Act
        var response = await _client.GetAsync("/api/proxies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ApiResponse<List<ProxyDto>>>(content);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetProxyById_NotFound_ShouldReturn404()
    {
        // Arrange
        EnsureEmptyConfig();

        // Act
        var response = await _client.GetAsync("/api/proxies/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddProxy_InvalidContentType_ShouldReturn415()
    {
        // Arrange
        var content = new StringContent("test", Encoding.UTF8, "text/plain");

        // Act
        var response = await _client.PostAsync("/api/proxies", content);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task AddProxy_MissingName_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new ProxyDto
        {
            RemoteHost = "1.2.3.4",
            RemotePort = 8080
        };
        var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/proxies", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync();
        Assert.Contains("Name", result);
    }

    [Fact]
    public async Task AddProxy_NameTooLong_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new ProxyDto
        {
            Name = new string('A', 201),
            RemoteHost = "1.2.3.4",
            RemotePort = 8080
        };
        var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/proxies", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddProxy_MissingRemoteHost_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new ProxyDto
        {
            Name = "Test",
            RemotePort = 8080
        };
        var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/proxies", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync();
        Assert.Contains("RemoteHost", result);
    }

    [Fact]
    public async Task AddProxy_InvalidRemotePort_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new ProxyDto
        {
            Name = "Test",
            RemoteHost = "1.2.3.4",
            RemotePort = 0
        };
        var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/proxies", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddProxy_InvalidLocalPort_ShouldReturnBadRequest()
    {
        // Arrange
        var dto = new ProxyDto
        {
            Name = "Test",
            RemoteHost = "1.2.3.4",
            RemotePort = 8080,
            LocalPort = 70000
        };
        var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/proxies", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddProxy_PortConflict_ShouldReturnConflict()
    {
        // Arrange
        EnsureConfigWithProxy(9000);
        var dto = new ProxyDto
        {
            Name = "Test2",
            RemoteHost = "5.6.7.8",
            RemotePort = 8080,
            LocalPort = 9000
        };
        var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/proxies", content);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddProxy_ValidInput_ShouldSucceed()
    {
        // Arrange
        EnsureEmptyConfig();
        var dto = new ProxyDto
        {
            Name = "Test",
            RemoteHost = "1.2.3.4",
            RemotePort = 8080,
            LocalPort = 0 // Auto-assign
        };
        var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/proxies", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<ProxyDto>>(result);
        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal("Test", apiResponse.Data.Name);
    }

    [Fact]
    public async Task DeleteProxy_NotFound_ShouldReturn404()
    {
        // Arrange
        EnsureEmptyConfig();

        // Act
        var response = await _client.DeleteAsync("/api/proxies/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProxy_Existing_ShouldSucceed()
    {
        // Arrange
        EnsureConfigWithProxy(9000);

        // Act
        var response = await _client.DeleteAsync("/api/proxies/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(result);
        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Success);
    }

    [Fact]
    public async Task StopProxy_NotFound_ShouldReturn404()
    {
        // Arrange
        EnsureEmptyConfig();

        // Act
        var response = await _client.PostAsync("/api/proxies/999/stop", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StopProxy_Existing_ShouldSucceed()
    {
        // Arrange
        EnsureConfigWithProxy(9000);

        // Act
        var response = await _client.PostAsync("/api/proxies/1/stop", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Stats_ShouldReturnStats()
    {
        // Arrange
        EnsureEmptyConfig();

        // Act
        var response = await _client.GetAsync("/api/stats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync();
        Assert.Contains("total", result.ToLower());
    }

    private void EnsureEmptyConfig()
    {
        var emptyConfig = new
        {
            version = "2.0",
            proxies = new List<object>()
        };
        File.WriteAllText(_testConfigPath, JsonSerializer.Serialize(emptyConfig));
    }

    private void EnsureConfigWithProxy(int localPort)
    {
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
                    localPort = localPort,
                    status = "stopped",
                    group = "",
                    createTime = DateTime.UtcNow
                }
            }
        };
        File.WriteAllText(_testConfigPath, JsonSerializer.Serialize(config));
    }
}
