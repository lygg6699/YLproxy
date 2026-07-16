using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using YLproxy.Infrastructure;
using YLproxy.Utils;

namespace YLproxy.Api;

public sealed class ApiServer
{
    private readonly string _configPath;
    private readonly ProxyConfig _proxyConfig;
    private readonly string _accessToken;
    private readonly int _port;
    private readonly ILog _logger;
    private WebApplication? _app;
    private Task? _runTask;
    private CancellationTokenSource? _cts;

    public int Port => _port;
    public bool IsRunning => _app is not null;

    public ApiServer(string configPath, ProxyConfig proxyConfig, int port, string accessToken)
    {
        _configPath = configPath;
        _proxyConfig = proxyConfig;
        _port = port;
        _accessToken = accessToken;
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
        });

        builder.Logging.ClearProviders();

        _app = builder.Build();

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
}
