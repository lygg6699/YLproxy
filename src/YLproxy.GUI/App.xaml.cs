using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using YLproxy.Core.PreFlight;
using YLproxy.Infrastructure;
using YLproxy.Utils;
using GlobalConfigService = YLproxy.Infrastructure.AppSettingsService;
using GlobalProxyConfig = YLproxy.Infrastructure.ProxyConfig;
using GlobalThreeProxyConfig = YLproxy.Infrastructure.ThreeProxyConfig;
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
        var settingsPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory,
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

        // Build DI container (Phase B2: interface alignment + startup chain closure)
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
            return sp.GetRequiredService<AppSettingsService>().GetSection<GlobalProxyConfig>("Proxy");
        });
        services.AddSingleton<GlobalThreeProxyConfig>(sp =>
        {
            return sp.GetRequiredService<AppSettingsService>().GetSection<GlobalThreeProxyConfig>("ThreeProxy");
        });

        // Core abstractions → adapters
        services.AddSingleton<Proxy.Abstractions.IProxyProcessManager, Proxy.ProxyProcessManagerAdapter>();
        services.AddSingleton<Core.Abstractions.IProxyTester, Core.ProxyTesterAdapter>();
        services.AddSingleton<Core.Abstractions.IProxyDataService>(sp =>
        {
            var cfg = sp.GetRequiredService<GlobalProxyConfig>();
            var configPath = PathResolver.ResolvePath(cfg.DataDirectory, cfg.ConfigFileName);
            return new Core.Config.ProxyDataService(configPath);
        });

        // Main VM
        services.AddTransient<MainViewModel>();

        var provider = services.BuildServiceProvider();

        // Manual startup window creation (since StartupUri removed)
        var vm = provider.GetRequiredService<MainViewModel>();
        var win = new MainWindow { DataContext = vm };
        win.Show();


    }

    protected override void OnExit(ExitEventArgs e)
    {
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
