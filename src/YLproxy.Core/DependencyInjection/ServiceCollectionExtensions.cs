using Microsoft.Extensions.DependencyInjection;
using YLproxy.Core.Abstractions;
using YLproxy.Infrastructure;
using YLproxy.Infrastructure.Abstractions;
using YLproxy.Proxy;
using YLproxy.Proxy.Abstractions;
using YLproxy.Utils;

namespace YLproxy.Core.DependencyInjection;

/// <summary>
/// Provides extension methods for registering YLproxy services with a DI container.
/// Supports Microsoft.Extensions.DependencyInjection (IServiceCollection).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all YLproxy core services into the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configFilePath">Optional path to the JSON configuration file.
    /// If not specified, defaults to "AppSettings.json".</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddYLproxyServices(
        this IServiceCollection services,
        string? configFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // --- Configuration ---
        var configPath = configFilePath ?? "AppSettings.json";
        services.AddSingleton<IConfigurationProvider>(_ =>
            new JsonConfigurationProvider(configPath, optional: false, watchChanges: true));
        services.AddSingleton<EnvironmentConfigurationProvider>();
        services.AddSingleton<ConfigurationManager>(sp =>
        {
            var manager = new ConfigurationManager();
            // Environment variables override JSON file settings
            manager.AddProvider(sp.GetRequiredService<EnvironmentConfigurationProvider>());
            manager.AddProvider(sp.GetRequiredService<IConfigurationProvider>());
            return manager;
        });

        // --- AppSettings Service ---
        services.AddSingleton<IAppSettingsService>(_ => new AppSettingsService(configPath));

        // --- Logging ---
        services.AddSingleton<ILogger>(_ => LoggerFactory.CreateLogger());

        // --- Proxy Services ---
        services.AddSingleton<ProxyRuntimeConfiguration>();
        services.AddSingleton<ProxyProcessManager>();
        services.AddSingleton<IProxyProcessManager>(sp =>
            new ProxyProcessManagerAdapter(sp.GetRequiredService<ProxyProcessManager>()));

        // --- Core Services ---
        services.AddSingleton<IProxyDataService>(_ =>
        {
            var dataPath = PathResolver.ResolvePath("data", "config.json");
            return new ProxyDataService(dataPath);
        });
        services.AddSingleton<IProxyTester, ProxyTesterAdapter>();

        return services;
    }

    /// <summary>
    /// Registers a simplified set of YLproxy services suitable for testing.
    /// Uses in-memory or mock-friendly registrations.
    /// </summary>
    public static IServiceCollection AddYLproxyTestServices(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<EnvironmentConfigurationProvider>();
        services.AddSingleton<ConfigurationManager>(sp =>
        {
            var manager = new ConfigurationManager { CacheEnabled = false };
            manager.AddProvider(sp.GetRequiredService<EnvironmentConfigurationProvider>());
            return manager;
        });

        services.AddSingleton<ProxyRuntimeConfiguration>();
        services.AddSingleton<ProxyProcessManager>();
        services.AddSingleton<IProxyProcessManager, ProxyProcessManagerAdapter>();
        services.AddSingleton<IProxyTester, ProxyTesterAdapter>();

        return services;
    }
}

