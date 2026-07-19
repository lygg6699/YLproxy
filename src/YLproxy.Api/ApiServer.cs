using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using YLproxy.Infrastructure;
using YLproxy.Utils;

namespace YLproxy.Api;

public sealed class ApiServer : IDisposable
{
    private readonly string _configPath;
    private readonly ProxyConfig _proxyConfig;
    private readonly string _accessToken;
    private readonly int _port;
    private readonly bool _enableSwagger;
    private readonly ILog _logger;
    private WebApplication? _app;
    private Task? _runTask;
    private CancellationTokenSource? _cts;

    public int Port => _port;
    public bool IsRunning => _app is not null;

    public ApiServer(string configPath, ProxyConfig proxyConfig, int port, string accessToken, bool enableSwagger = false)
    {
        _configPath = configPath;
        _proxyConfig = proxyConfig;
        _port = port;
        _accessToken = accessToken;
        _enableSwagger = enableSwagger;
        _logger = LogFactory.CreateLogger();
    }

    public async Task StartAsync()
    {
        if (_app is not null)
        {
            _logger.Warn($"API server already running on port {_port}");
            return;
        }

        _cts = new CancellationTokenSource();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(options =>
        {
            options.Listen(System.Net.IPAddress.Loopback, _port);
            options.Limits.MaxRequestBodySize = 64 * 1024; // 64 KB
        });

        builder.Logging.ClearProviders();

        if (_enableSwagger)
        {
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "YLproxy API",
                    Version = "v1",
                    Description = "代理管理 REST API — 配置、测试、启停",
                });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "输入 Bearer token",
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                        },
                        Array.Empty<string>()
                    },
                });
            });
        }

        _app = builder.Build();

        if (_enableSwagger)
        {
            _app.UseSwagger(c =>
            {
                c.RouteTemplate = "swagger/{documentName}/swagger.json";
            });
            _app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "YLproxy API v1");
                c.RoutePrefix = "swagger";
            });
        }

        _app.UseMiddleware<ApiAuthMiddleware>(_accessToken);
        ApiEndpoints.Map(_app, _configPath, _proxyConfig);

        _logger.Info($"API server starting on http://127.0.0.1:{_port}");

        await _app.StartAsync(_cts.Token);
        _runTask = _app.WaitForShutdownAsync(_cts.Token);

        _logger.Info($"API server started on http://127.0.0.1:{_port}");
    }

    public async Task StopAsync()
    {
        if (_app is null) return;

        _logger.Info("API server stopping...");

        _cts?.Cancel();

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }

        try { await (_runTask ?? Task.CompletedTask); } catch (OperationCanceledException) { }

        _logger.Info("API server stopped.");
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}
