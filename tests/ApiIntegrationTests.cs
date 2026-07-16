using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using YLproxy.Api;
using YLproxy.Infrastructure;

namespace YLproxy.Tests;

public sealed class ApiIntegrationTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly int _port = 9101;
    private readonly string _token = "test-token-123";
    private ApiServer? _server;

    public ApiIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ylproxy_api_test_{Guid.NewGuid():N}");
        _configPath = Path.Combine(_tempDir, "test_config.json");
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDir);

        // Create a minimal config with test proxies
        var cfg = new YLproxy.Models.AppConfig();
        cfg.Proxies.Add(new YLproxy.Models.ProxyItem
        {
            Id = 1, Name = "TestProxy1", RemoteHost = "1.2.3.4", RemotePort = 8080,
            Username = "user1", Password = "pass1",
            LocalHost = "0.0.0.0", LocalPort = 9001,
            Status = YLproxy.Models.ProxyStatus.Stopped
        });
        cfg.Proxies.Add(new YLproxy.Models.ProxyItem
        {
            Id = 2, Name = "TestProxy2", RemoteHost = "5.6.7.8", RemotePort = 3128,
            LocalHost = "0.0.0.0", LocalPort = 9002,
            Status = YLproxy.Models.ProxyStatus.Stopped
        });

        var json = System.Text.Json.JsonSerializer.Serialize(cfg);
        await File.WriteAllTextAsync(_configPath, json);

        var proxyCfg = new ProxyConfig { DataDirectory = _tempDir, ConfigFileName = "test_config.json" };

        _server = new ApiServer(_configPath, proxyCfg, _port, _token);
        await _server.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_server is not null)
            await _server.StopAsync();

        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private HttpClient CreateClient()
    {
        var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return client;
    }

    // === Health ===

    [Fact]
    public async Task HealthEndpoint_ShouldReturnOkWithoutAuth()
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // === Auth ===

    [Fact]
    public async Task ProtectedEndpoint_ShouldRejectWithoutAuth()
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
        var response = await client.GetAsync("/api/proxies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_ShouldRejectWithInvalidToken()
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");
        var response = await client.GetAsync("/api/proxies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // === CRUD ===

    [Fact]
    public async Task GetProxies_ShouldReturnAll()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/proxies");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProxyDto>>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal(2, body.Data!.Count);
    }

    [Fact]
    public async Task GetProxyById_ShouldReturnProxy()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/proxies/1");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProxyDto>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("TestProxy1", body.Data!.Name);
    }

    [Fact]
    public async Task GetProxyById_NotFound_ShouldReturn404()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/proxies/999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddProxy_ShouldCreateNew()
    {
        using var client = CreateClient();
        var dto = new ProxyDto
        {
            Name = "NewProxy",
            RemoteHost = "10.0.0.1",
            RemotePort = 8080,
            Username = "u", Password = "p"
        };

        var response = await client.PostAsJsonAsync("/api/proxies", dto);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Verify it appears in list
        var listResponse = await client.GetAsync("/api/proxies");
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProxyDto>>>();
        Assert.NotNull(list);
        Assert.Contains(list!.Data!, p => p.Name == "NewProxy");
    }

    [Fact]
    public async Task DeleteProxy_ShouldRemove()
    {
        using var client = CreateClient();
        var response = await client.DeleteAsync("/api/proxies/2");
        response.EnsureSuccessStatusCode();

        // Verify it's gone
        var getResponse = await client.GetAsync("/api/proxies/2");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // === Stats ===

    [Fact]
    public async Task Stats_ShouldReturnCounts()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/stats");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<Dictionary<string, int>>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.True(body.Data!.ContainsKey("total"));
        Assert.True(body.Data!.ContainsKey("stopped"));
    }
}
