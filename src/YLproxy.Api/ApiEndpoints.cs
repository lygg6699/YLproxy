using YLproxy.Core.Config;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Proxy;

namespace YLproxy.Api;

public static class ApiEndpoints
{
    private static readonly ILog _logger = LogFactory.CreateLogger();

    public static void Map(WebApplication app, string configPath, ProxyConfig proxyConfig)
    {
        ProxyDataService CreateSvc() => new(configPath, skipPathValidation: true);

        var group = app.MapGroup("/api");

        // Health
        group.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }));

        // List all
        group.MapGet("/proxies", () =>
        {
            try
            {
                var svc = CreateSvc();
                var cfg = svc.Load();
                var dtos = cfg.Proxies.Select(MapToDto).ToList();
                return Results.Ok(ApiResponse<List<ProxyDto>>.Ok(dtos));
            }
            catch (Exception ex) { _logger.Error($"GET /proxies: {ex.Message}"); return Results.Ok(ApiResponse<List<ProxyDto>>.Fail(ex.Message)); }
        });

        // Get single
        group.MapGet("/proxies/{id:int}", (int id) =>
        {
            try
            {
                var svc = CreateSvc();
                var cfg = svc.Load();
                var proxy = cfg.Proxies.FirstOrDefault(p => p.Id == id);
                if (proxy is null)
                    return Results.NotFound(ApiResponse<object>.Fail("Proxy not found"));
                return Results.Ok(ApiResponse<ProxyDto>.Ok(MapToDto(proxy)));
            }
            catch (Exception ex) { _logger.Error($"GET /proxies/{id}: {ex.Message}"); return Results.Ok(ApiResponse<object>.Fail(ex.Message)); }
        });

        // Add
        group.MapPost("/proxies", (ProxyDto dto) =>
        {
            try
            {
                var svc = CreateSvc();
                var cfg = svc.Load();
                var existingPorts = new HashSet<int>(cfg.Proxies.Select(p => p.LocalPort));

                var nextId = cfg.Proxies.Count == 0 ? 1 : cfg.Proxies.Max(p => p.Id) + 1;

                int localPort;
                if (dto.LocalPort > 0)
                {
                    if (existingPorts.Contains(dto.LocalPort))
                        return Results.Conflict(ApiResponse<ProxyDto>.Fail("Local port already in use"));
                    localPort = dto.LocalPort;
                }
                else
                {
                    localPort = proxyConfig.PortRangeStart;
                    while (existingPorts.Contains(localPort))
                    {
                        localPort++;
                        if (localPort > proxyConfig.PortRangeEnd)
                            return Results.Conflict(ApiResponse<ProxyDto>.Fail("Port range exhausted"));
                    }
                }

                var item = new ProxyItem
                {
                    Id = nextId,
                    Name = dto.Name ?? $"Proxy-{nextId}",
                    RemoteHost = dto.RemoteHost,
                    RemotePort = dto.RemotePort,
                    Username = dto.Username ?? string.Empty,
                    Password = dto.Password ?? string.Empty,
                    LocalHost = "0.0.0.0",
                    LocalPort = localPort,
                    Status = ProxyStatus.Stopped,
                    CreateTime = DateTime.UtcNow
                };

                cfg.Proxies.Add(item);
                svc.Save(cfg);
                _logger.Info($"API: added proxy {item.Id} ({item.RemoteHost}:{item.RemotePort})");
                return Results.Created($"/api/proxies/{item.Id}", ApiResponse<ProxyDto>.Ok(MapToDto(item)));
            }
            catch (Exception ex) { _logger.Error($"POST /proxies: {ex.Message}"); return Results.Ok(ApiResponse<ProxyDto>.Fail(ex.Message)); }
        });

        // Delete
        group.MapDelete("/proxies/{id:int}", (int id) =>
        {
            try
            {
                var svc = CreateSvc();
                var cfg = svc.Load();
                var removed = cfg.Proxies.RemoveAll(p => p.Id == id);
                if (removed == 0)
                    return Results.NotFound(ApiResponse<object>.Fail("Proxy not found"));

                try { ProxyProcessManager.Stop(new ProxyItem { Id = id }); } catch { }

                svc.Save(cfg);
                _logger.Info($"API: deleted proxy {id}");
                return Results.Ok(ApiResponse<object>.Ok(new { deleted = true }));
            }
            catch (Exception ex) { _logger.Error($"DELETE /proxies/{id}: {ex.Message}"); return Results.Ok(ApiResponse<object>.Fail(ex.Message)); }
        });

        // Test
        group.MapPost("/proxies/{id:int}/test", async (int id) =>
        {
            try
            {
                var svc = CreateSvc();
                var cfg = svc.Load();
                var proxy = cfg.Proxies.FirstOrDefault(p => p.Id == id);
                if (proxy is null)
                    return Results.NotFound(ApiResponse<ProxyTestResult>.Fail("Proxy not found"));

                var result = await YLproxy.Core.ProxyTester.TestAsync(
                    proxy.RemoteHost, proxy.RemotePort,
                    string.IsNullOrWhiteSpace(proxy.Username) ? null : proxy.Username,
                    string.IsNullOrWhiteSpace(proxy.Password) ? null : proxy.Password);

                return Results.Ok(ApiResponse<ProxyTestResult>.Ok(new ProxyTestResult
                {
                    Success = result.Success,
                    LatencyMs = result.LatencyMs,
                    Error = result.Error
                }));
            }
            catch (Exception ex) { _logger.Error($"POST /proxies/{id}/test: {ex.Message}"); return Results.Ok(ApiResponse<ProxyTestResult>.Fail(ex.Message)); }
        });

        // Start
        group.MapPost("/proxies/{id:int}/start", (int id) =>
        {
            try
            {
                var svc = CreateSvc();
                var cfg = svc.Load();
                var proxy = cfg.Proxies.FirstOrDefault(p => p.Id == id);
                if (proxy is null)
                    return Results.NotFound(ApiResponse<object>.Fail("Proxy not found"));

                proxy.Status = ProxyStatus.Running;
                ProxyProcessManager.Start(proxy);
                svc.Save(cfg);
                _logger.Info($"API: started proxy {id}");
                return Results.Ok(ApiResponse<ProxyDto>.Ok(MapToDto(proxy)));
            }
            catch (Exception ex) { _logger.Error($"POST /proxies/{id}/start: {ex.Message}"); return Results.Ok(ApiResponse<object>.Fail(ex.Message)); }
        });

        // Stop
        group.MapPost("/proxies/{id:int}/stop", (int id) =>
        {
            try
            {
                var svc = CreateSvc();
                var cfg = svc.Load();
                var proxy = cfg.Proxies.FirstOrDefault(p => p.Id == id);
                if (proxy is null)
                    return Results.NotFound(ApiResponse<object>.Fail("Proxy not found"));

                ProxyProcessManager.Stop(proxy);
                proxy.Status = ProxyStatus.Stopped;
                svc.Save(cfg);
                _logger.Info($"API: stopped proxy {id}");
                return Results.Ok(ApiResponse<ProxyDto>.Ok(MapToDto(proxy)));
            }
            catch (Exception ex) { _logger.Error($"POST /proxies/{id}/stop: {ex.Message}"); return Results.Ok(ApiResponse<object>.Fail(ex.Message)); }
        });

        // Dashboard stats
        group.MapGet("/stats", () =>
        {
            try
            {
                var svc = CreateSvc();
                var cfg = svc.Load();
                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    total = cfg.Proxies.Count,
                    running = cfg.Proxies.Count(p => p.Status == ProxyStatus.Running),
                    stopped = cfg.Proxies.Count(p => p.Status == ProxyStatus.Stopped),
                    failed = cfg.Proxies.Count(p => p.Status == ProxyStatus.Failed)
                }));
            }
            catch (Exception ex) { _logger.Error($"GET /stats: {ex.Message}"); return Results.Ok(ApiResponse<object>.Fail(ex.Message)); }
        });
    }

    private static ProxyDto MapToDto(ProxyItem p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        RemoteHost = p.RemoteHost,
        RemotePort = p.RemotePort,
        Username = p.Username,
        Password = string.IsNullOrWhiteSpace(p.Password) ? string.Empty : "****",
        LocalHost = p.LocalHost,
        LocalPort = p.LocalPort,
        Status = p.Status.ToString(),
        CreateTime = p.CreateTime
    };
}
