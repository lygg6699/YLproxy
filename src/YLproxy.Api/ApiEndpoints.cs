using YLproxy.Core.Config;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Proxy;
using Microsoft.AspNetCore.Http;

namespace YLproxy.Api;

public static class ApiEndpoints
{
    private static readonly ILog _logger = LogFactory.CreateLogger();

    private static bool IsJsonContentType(HttpRequest request) =>
        request.Headers.ContentType.FirstOrDefault() is string ct &&
        ct.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);

    public static void Map(WebApplication app, string configPath, ProxyConfig proxyConfig)
    {
        ProxyDataService CreateSvc() => new(configPath, skipPathValidation: true);

        var group = app.MapGroup("/api");

        // Health
        group.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }))
            .WithTags("Health")
            .Produces(200);

        // List all
        group.MapGet("/proxies", () =>
        {
            try
            {
                var svc = CreateSvc();
                var cfg = svc.Load();
                var dtos = cfg.Proxies.Select(MapToDto).ToList();
                return Results.Ok(ApiResponse.Ok(dtos));
            }
            catch (Exception ex) { _logger.Error($"GET /proxies: {ex.Message}"); return Results.InternalServerError(ApiResponse<List<ProxyDto>>.Fail(ex.Message, "INTERNAL_ERROR")); }
        })
            .WithTags("Proxies")
            .Produces<ApiResponse<List<ProxyDto>>>(200);

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
                return Results.Ok(ApiResponse.Ok(MapToDto(proxy)));
            }
            catch (Exception ex) { _logger.Error($"GET /proxies/{id}: {ex.Message}"); return Results.InternalServerError(ApiResponse<object>.Fail(ex.Message, "INTERNAL_ERROR")); }
        })
            .WithTags("Proxies")
            .Produces<ApiResponse<ProxyDto>>(200)
            .Produces(404);

        // Add
        group.MapPost("/proxies", (ProxyDto dto, HttpContext http) =>
        {
            try
            {
                if (!IsJsonContentType(http.Request))
                    return Results.StatusCode(415); // Unsupported Media Type

                // --- input validation ---
                if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 200)
                    return Results.BadRequest(ApiResponse<ProxyDto>.Fail("Name is required (max 200 chars)"));

                if (string.IsNullOrWhiteSpace(dto.RemoteHost))
                    return Results.BadRequest(ApiResponse<ProxyDto>.Fail("RemoteHost is required"));

                if (dto.RemotePort < 1 || dto.RemotePort > 65535)
                    return Results.BadRequest(ApiResponse<ProxyDto>.Fail("RemotePort must be 1-65535"));

                if (dto.LocalPort < 0 || dto.LocalPort > 65535)
                    return Results.BadRequest(ApiResponse<ProxyDto>.Fail("LocalPort must be 0 (auto) or 1-65535"));
                // --- end input validation ---

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
                    CreateTime = DateTime.UtcNow,
                    Group = dto.Group ?? string.Empty
                };

                cfg.Proxies.Add(item);
                svc.Save(cfg);
                _logger.Info($"API: added proxy {item.Id} ({item.RemoteHost}:{item.RemotePort})");
                return Results.Created($"/api/proxies/{item.Id}", ApiResponse.Ok(MapToDto(item)));
            }
            catch (Exception ex) { _logger.Error($"POST /proxies: {ex.Message}"); return Results.InternalServerError(ApiResponse<ProxyDto>.Fail(ex.Message, "INTERNAL_ERROR")); }
        })
            .WithTags("Proxies")
            .Produces<ApiResponse<ProxyDto>>(201)
            .Produces<ApiResponse<ProxyDto>>(409);

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

                try { ProxyProcessManager.Stop(new ProxyItem { Id = id }); } catch (Exception ex) { _logger.Warn($"Failed to stop proxy {id} during delete: {ex.Message}"); }

                svc.Save(cfg);
                _logger.Info($"API: deleted proxy {id}");
                return Results.Ok(ApiResponse.Ok(new { deleted = true }));
            }
            catch (Exception ex) { _logger.Error($"DELETE /proxies/{id}: {ex.Message}"); return Results.InternalServerError(ApiResponse<object>.Fail(ex.Message, "INTERNAL_ERROR")); }
        })
            .WithTags("Proxies")
            .Produces(200)
            .Produces(404);

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
            catch (Exception ex) { _logger.Error($"POST /proxies/{id}/test: {ex.Message}"); return Results.InternalServerError(ApiResponse<ProxyTestResult>.Fail(ex.Message, "INTERNAL_ERROR")); }
        })
            .WithTags("Proxies")
            .Produces<ApiResponse<ProxyTestResult>>(200);

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
                return Results.Ok(ApiResponse.Ok(MapToDto(proxy)));
            }
            catch (Exception ex) { _logger.Error($"POST /proxies/{id}/start: {ex.Message}"); return Results.InternalServerError(ApiResponse<object>.Fail(ex.Message, "INTERNAL_ERROR")); }
        })
            .WithTags("Proxies")
            .Produces<ApiResponse<ProxyDto>>(200);

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
                return Results.Ok(ApiResponse.Ok(MapToDto(proxy)));
            }
            catch (Exception ex) { _logger.Error($"POST /proxies/{id}/stop: {ex.Message}"); return Results.InternalServerError(ApiResponse<object>.Fail(ex.Message, "INTERNAL_ERROR")); }
        })
            .WithTags("Proxies")
            .Produces<ApiResponse<ProxyDto>>(200);

        // Dashboard stats
        group.MapGet("/stats", () =>
        {
            try
            {
                var svc = CreateSvc();
                var cfg = svc.Load();
                return Results.Ok(ApiResponse.Ok(new
                {
                    total = cfg.Proxies.Count,
                    running = cfg.Proxies.Count(p => p.Status == ProxyStatus.Running),
                    stopped = cfg.Proxies.Count(p => p.Status == ProxyStatus.Stopped),
                    failed = cfg.Proxies.Count(p => p.Status == ProxyStatus.Failed)
                }));
            }
            catch (Exception ex) { _logger.Error($"GET /stats: {ex.Message}"); return Results.InternalServerError(ApiResponse<object>.Fail(ex.Message, "INTERNAL_ERROR")); }
        })
            .WithTags("Stats")
            .Produces(200);
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
        CreateTime = p.CreateTime,
        Group = p.Group
    };
}
