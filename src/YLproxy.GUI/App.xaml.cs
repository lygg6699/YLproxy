using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using YLproxy.Api;
using YLproxy.Core.PreFlight;
using YLproxy.GUI.Services;
using YLproxy.Infrastructure;
using YLproxy.Models.Config;
using YLproxy.Utils;
using GlobalConfigService = YLproxy.Infrastructure.AppSettingsService;
using GlobalProxyConfig = YLproxy.Models.Config.ProxyConfig;
using GlobalThreeProxyConfig = YLproxy.Models.Config.ThreeProxyConfig;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;


namespace YLproxy.GUI;

public partial class App : Application
{
    private ILogger? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _logger = LoggerFactory.CreateLogger();

        // 加载默认主题
        ThemeService.Instance.ApplyTheme("DarkTheme");

        ExceptionHandler.OnUserNotification = (context, message) =>
        {
            Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"{context}:\n\n{message}",
                    "YLproxy - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            });
        };

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _logger.Info("Application started.");

        // Pre-flight checks
        var preFlight = PreFlightChecker.Run();
        if (!preFlight.Passed)
        {
            var errors = string.Join("\n\n", preFlight.Errors.Select((err, i) => $"{i + 1}. {err}"));
            _logger.Error($"Pre-flight check failed:\n{errors}");
            MessageBox.Show(
                $"YLproxy 启动前检查发现以下问题:\n\n{errors}\n\n请修复后重新启动。",
                "YLproxy - 启动检查失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Shutdown(1);
            return;
        }

        foreach (var w in preFlight.Warnings)
            _logger.Warn($"Pre-flight warning: {w}");

        // Build DI container (Phase A skeleton)
        var settingsPath = PathHelper.Combine(
            Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory,
            "AppSettings.json");
        try
        {
            var svc = new AppSettingsService(settingsPath);
            var cfg = svc.GetConfig();
            if (cfg?.Startup.AutoStart == true)
            {
                AutoStartService.SetAutoStart(true);
                _logger.Info("Auto-start registered in Windows Startup.");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to configure auto-start: {ex.Message}");
        }

        // Build DI container
        var services = new ServiceCollection();

        // Logging
        services.AddSingleton<ILogger>(_ => _logger!);

        // AppSettings
        services.AddSingleton(_ =>
        {
            var svc = new AppSettingsService(settingsPath);
            return svc;
        });

        // Config sections
        services.AddSingleton<GlobalConfigService>(sp => sp.GetRequiredService<AppSettingsService>());
        services.AddSingleton<GlobalProxyConfig>(sp =>
        {
            return sp.GetRequiredService<AppSettingsService>().GetProxyConfig();
        });
        services.AddSingleton<GlobalThreeProxyConfig>(sp =>
        {
            return sp.GetRequiredService<AppSettingsService>().GetThreeProxyConfig();
        });

        // Core abstractions → adapters
        services.AddSingleton<Proxy.Abstractions.IProxyProcessManager, Proxy.ProxyProcessManagerAdapter>();
        services.AddSingleton<Core.Abstractions.IProxyTester, Core.ProxyTesterAdapter>();
        services.AddSingleton<Core.Abstractions.IProxyDataService>(sp =>
        {
            var cfg = sp.GetRequiredService<GlobalProxyConfig>();
            var configPath = PathResolver.ResolvePath(cfg.DataDirectory, cfg.ConfigFileName);
            return new Core.ProxyDataService(configPath);
        });

        // API Server
        services.AddSingleton<ApiServer>(sp =>
        {
            var settingsService = sp.GetRequiredService<AppSettingsService>();
            var apiConfig = settingsService.GetApiConfig();
            var proxyConfig = settingsService.GetProxyConfig();
            var configPath = PathResolver.ResolvePath(proxyConfig.DataDirectory, proxyConfig.ConfigFileName);

            var isProduction = string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                "Production", StringComparison.OrdinalIgnoreCase);

            return new ApiServer(
                configPath: configPath,
                proxyConfig: proxyConfig,
                port: apiConfig.Port,
                accessToken: apiConfig.AccessToken,
                enableSwagger: !isProduction);
        });

        // Main VM
        services.AddTransient<MainViewModel>();

        var provider = services.BuildServiceProvider();

        // Manual startup window creation
        var vm = provider.GetRequiredService<MainViewModel>();
        var win = new MainWindow { DataContext = vm };
        win.Show();

        // 启动 API 服务器（非阻塞）
        _ = Task.Run(async () =>
        {
            try
            {
                var apiServer = provider.GetRequiredService<ApiServer>();
                await apiServer.StartAsync();
                _logger?.Info($"API server started on http://127.0.0.1:{apiServer.Port}");

                await Current.Dispatcher.BeginInvoke(() =>
                {
                    vm.ApiStatus = "Running";
                    vm.Dashboard.UpdateApiStatus("Running", apiServer.Port);
                });
            }
            catch (Exception ex)
            {
                _logger?.Warn($"API server failed to start: {ex.Message}");
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 停止 API 服务器（作为 ShutdownAsync 的兜底）
        try
        {
            var apiServer = (ApiServer?)Current?.Dispatcher?.Invoke(() =>
                ((MainWindow?)Current?.MainWindow)?.DataContext is MainViewModel vm
                    ? vm.GetApiServer()
                    : null);

            if (apiServer?.IsRunning == true)
            {
                apiServer.StopAsync().GetAwaiter().GetResult();
                _logger?.Info("API server stopped.");
            }
        }
        catch (Exception ex)
        {
            _logger?.Warn($"Failed to stop API server: {ex.Message}");
        }

        _logger?.Info($"Application exiting with code {e.ApplicationExitCode}.");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Fatal($"Unhandled UI exception: {e.Exception.Message}", e.Exception);
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "YLproxy - Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            _logger?.Fatal($"Unhandled background exception (terminating={e.IsTerminating}): {ex.Message}", ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.Error($"Unobserved task exception: {e.Exception.Message}", e.Exception);
        e.SetObserved();
    }
}
