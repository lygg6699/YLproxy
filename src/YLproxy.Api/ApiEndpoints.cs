
using YLproxy.Core;
using YLproxy.Core.Config;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Models.Config;
using YLproxy.Proxy;
using Microsoft.AspNetCore.Http;

namespace YLproxy.Api;

public static class ApiEndpoints
{
    private static readonly ILogger _logger = LogFactory.CreateLogger();

    private static bool IsJsonContentType(HttpRequest request) =>
        request.Headers.ContentType.FirstOrDefault() is string ct &&
        ct.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Wraps a synchronous endpoint handler with try-catch logging.
    /// </summary>
    private static IResult SafeExecute(Func<IResult> handler, string operationName, HttpContext? http = null)
    {
        try
        {
            return handler();
        }
        catch (Exception ex)
        {
            LogWithAudit(_logger, $"{operationName}: {ex.Message}", "ERROR", http);
            return Results.InternalServerError(ApiResponse.Fail<object>(ex.Message, "INTERNAL_ERROR"));
        }
    }

    /// <summary>
    /// Wraps an async endpoint handler with try-catch logging.
    /// </summary>
    private static async Task<IResult> SafeExecuteAsync(Func<Task<IResult>> handler, string operationName, HttpContext? http = null)
    {
        try
        {
            return await handler();
        }
        catch (Exception ex)
        {
            LogWithAudit(_logger, $"{operationName}: {ex.Message}", "ERROR", http);
            return Results.InternalServerError(ApiResponse.Fail<object>(ex.Message, "INTERNAL_ERROR"));
        }
    }

    /// <summary>
    /// Logs a message with optional source IP and User-Agent for audit trail.
    /// </summary>
    private static void LogWithAudit(ILogger logger, string message, string level, HttpContext? http)
    {
        var ip = http?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var ua = http?.Request.Headers.UserAgent.FirstOrDefault() ?? "unknown";
        var audit = $"{message} [source={ip}, ua={ua}]";
        switch (level)
        {
            case "ERROR": logger.Error(audit); break;
            case "WARN": logger.Warn(audit); break;
            default: logger.Info(audit); break;
        }
    }

    public static void Map(WebApplication app, string configPath, ProxyConfig proxyConfig)
    {
        ProxyDataService CreateSvc() => new(configPath, skipPathValidation: true);

        var group = app.MapGroup("/api");

        // Health
        group.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }))
            .WithTags("Health")
            .Produces(200);

        // List all
        group.MapGet("/proxies", () => SafeExecute(() =>
        {
            var svc = CreateSvc();
            var cfg = svc.Load();
            var dtos = cfg.Proxies.Select(MapToDto).ToList();
            return Results.Ok(ApiResponse.Ok(dtos));
        }, "GET /proxies"))
            .WithTags("Proxies")
            .Produces<ApiResponse<List<ProxyDto>>>(200);

        // Get single
        group.MapGet("/proxies/{id:int}", (int id) => SafeExecute(() =>
        {
            var svc = CreateSvc();
            var cfg = svc.Load();
            var proxy = cfg.Proxies.FirstOrDefault(p => p.Id == id);
            if (proxy is null)
                return Results.NotFound(ApiResponse.Fail<object>("Proxy not found"));
            return Results.Ok(ApiResponse.Ok(MapToDto(proxy)));
        }, $"GET /proxies/{id}"))
            .WithTags("Proxies")
            .Produces<ApiResponse<ProxyDto>>(200)
            .Produces(404);

        // Add
        group.MapPost("/proxies", (ProxyDto dto, HttpContext http) => SafeExecute(() =>
        {
            if (!IsJsonContentType(http.Request))
                return Results.StatusCode(415); // Unsupported Media Type

            // --- input validation ---
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 200)
                return Results.BadRequest(ApiResponse.Fail<ProxyDto>("Name is required (max 200 chars)"));

            if (string.IsNullOrWhiteSpace(dto.RemoteHost))
                return Results.BadRequest(ApiResponse.Fail<ProxyDto>("RemoteHost is required"));

            if (dto.RemotePort < 1 || dto.RemotePort > 65535)
                return Results.BadRequest(ApiResponse.Fail<ProxyDto>("RemotePort must be 1-65535"));

            if (dto.LocalPort < 0 || dto.LocalPort > 65535)
                return Results.BadRequest(ApiResponse.Fail<ProxyDto>("LocalPort must be 0 (auto) or 1-65535"));
            // --- end input validation ---

            var svc = CreateSvc();
            var cfg = svc.Load();
            var existingPorts = new HashSet<int>(cfg.Proxies.Select(p => p.LocalPort));

            var nextId = cfg.Proxies.Count == 0 ? 1 : cfg.Proxies.Max(p => p.Id) + 1;

            int localPort;
            if (dto.LocalPort > 0)
            {
                if (existingPorts.Contains(dto.LocalPort))
                    return Results.Conflict(ApiResponse.Fail<ProxyDto>("Local port already in use"));
                localPort = dto.LocalPort;
            }
            else
            {
                localPort = proxyConfig.PortRangeStart;
                while (existingPorts.Contains(localPort))
                {
                    localPort++;
                    if (localPort > proxyConfig.PortRangeEnd)
                        return Results.Conflict(ApiResponse.Fail<ProxyDto>("Port range exhausted"));
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
        }, "POST /proxies"))
            .WithTags("Proxies")
            .Produces<ApiResponse<ProxyDto>>(201)
            .Produces<ApiResponse<ProxyDto>>(409);

        // Delete
        group.MapDelete("/proxies/{id:int}", (int id) => SafeExecute(() =>
        {
            var svc = CreateSvc();
            var cfg = svc.Load();
            var removed = cfg.Proxies.RemoveAll(p => p.Id == id);
            if (removed == 0)
                return Results.NotFound(ApiResponse.Fail<object>("Proxy not found"));
            try { ProxyProcessManager.Default.Stop(new ProxyItem { Id = id }); } catch (Exception ex) { _logger.Warn($"Failed to stop proxy {id} during delete: {ex.Message}"); }

            svc.Save(cfg);
            _logger.Info($"API: deleted proxy {id}");
            return Results.Ok(ApiResponse.Ok(new { deleted = true }));
        }, $"DELETE /proxies/{id}"))
            .WithTags("Proxies")
            .Produces(200)
            .Produces(404);

        // Test
        group.MapPost("/proxies/{id:int}/test", async (int id) => await SafeExecuteAsync(async () =>
        {
            var svc = CreateSvc();
            var cfg = svc.Load();
            var proxy = cfg.Proxies.FirstOrDefault(p => p.Id == id);
            if (proxy is null)
                return Results.NotFound(ApiResponse.Fail<ProxyTestResult>("Proxy not found"));

            var result = await YLproxy.Core.ProxyTester.TestAsync(
                proxy.RemoteHost, proxy.RemotePort,
                string.IsNullOrWhiteSpace(proxy.Username) ? null : proxy.Username,
                string.IsNullOrWhiteSpace(proxy.Password) ? null : proxy.Password);

            return Results.Ok(ApiResponse.Ok<ProxyTestResult>(new ProxyTestResult
            {
                Success = result.Success,
                LatencyMs = result.LatencyMs,
                Error = result.Error
            }));
        }, $"POST /proxies/{id}/test"))
            .WithTags("Proxies")
            .Produces<ApiResponse<ProxyTestResult>>(200);

        // Start
        group.MapPost("/proxies/{id:int}/start", (int id) => SafeExecute(() =>
        {
            var svc = CreateSvc();
            var cfg = svc.Load();
            var proxy = cfg.Proxies.FirstOrDefault(p => p.Id == id);
            if (proxy is null)
                return Results.NotFound(ApiResponse.Fail<object>("Proxy not found"));

            proxy.Status = ProxyStatus.Running;
            ProxyProcessManager.Default.Start(proxy);
            svc.Save(cfg);
            _logger.Info($"API: started proxy {id}");
            return Results.Ok(ApiResponse.Ok(MapToDto(proxy)));
        }, $"POST /proxies/{id}/start"))
            .WithTags("Proxies")
            .Produces<ApiResponse<ProxyDto>>(200);

        // Stop
        group.MapPost("/proxies/{id:int}/stop", (int id) => SafeExecute(() =>
        {
            var svc = CreateSvc();
            var cfg = svc.Load();
            var proxy = cfg.Proxies.FirstOrDefault(p => p.Id == id);
            if (proxy is null)
                return Results.NotFound(ApiResponse.Fail<object>("Proxy not found"));

            ProxyProcessManager.Default.Stop(proxy);
            proxy.Status = ProxyStatus.Stopped;
            svc.Save(cfg);
            _logger.Info($"API: stopped proxy {id}");
            return Results.Ok(ApiResponse.Ok(MapToDto(proxy)));
        }, $"POST /proxies/{id}/stop"))
            .WithTags("Proxies")
            .Produces<ApiResponse<ProxyDto>>(200);

        // Dashboard stats
        group.MapGet("/stats", () => SafeExecute(() =>
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
        }, "GET /stats"))
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
