using Microsoft.Extensions.DependencyInjection;
using YLproxy.Core;
using YLproxy.Core.Abstractions;
using YLproxy.Core.DependencyInjection;
using YLproxy.Infrastructure;
using YLproxy.Infrastructure.Abstractions;
using YLproxy.Proxy;
using YLproxy.Proxy.Abstractions;

namespace YLproxy.Tests;

[Trait("Category", "Unit")]
public class DependencyInjectionTests
{
    [Fact]
    public void AddYLproxyServices_RegistersConfigurationManager()
    {
        var services = new ServiceCollection();
        services.AddYLproxyTestServices();

        var sp = services.BuildServiceProvider();
        var manager = sp.GetService<ConfigurationManager>();

        Assert.NotNull(manager);
    }

    [Fact]
    public void AddYLproxyServices_RegistersProxyProcessManager()
    {
        var services = new ServiceCollection();
        services.AddYLproxyTestServices();

        var sp = services.BuildServiceProvider();
        var ppm = sp.GetService<ProxyProcessManager>();

        Assert.NotNull(ppm);
    }

    [Fact]
    public void AddYLproxyServices_RegistersIProxyProcessManager()
    {
        var services = new ServiceCollection();
        services.AddYLproxyTestServices();

        var sp = services.BuildServiceProvider();
        var ppm = sp.GetService<YLproxy.Proxy.Abstractions.IProxyProcessManager>();

        Assert.NotNull(ppm);
        Assert.IsType<ProxyProcessManagerAdapter>(ppm);
    }

    [Fact]
    public void AddYLproxyServices_RegistersIProxyTester()
    {
        var services = new ServiceCollection();
        services.AddYLproxyTestServices();

        var sp = services.BuildServiceProvider();
        var tester = sp.GetService<IProxyTester>();

        Assert.NotNull(tester);
        Assert.IsType<ProxyTesterAdapter>(tester);
    }

    [Fact]
    public void AddYLproxyServices_ConfigurationManager_HasEnvironmentProvider()
    {
        var services = new ServiceCollection();
        services.AddYLproxyTestServices();

        var sp = services.BuildServiceProvider();
        var manager = sp.GetRequiredService<ConfigurationManager>();

        Assert.NotEmpty(manager.Providers);
        Assert.Contains(manager.Providers, p => p.Name == "EnvironmentVariables");
    }

    [Fact]
    public void AddYLproxyServices_ConfigurationManager_CacheDisabledInTestMode()
    {
        var services = new ServiceCollection();
        services.AddYLproxyTestServices();

        var sp = services.BuildServiceProvider();
        var manager = sp.GetRequiredService<ConfigurationManager>();

        Assert.False(manager.CacheEnabled);
    }

    [Fact]
    public void AddYLproxyServices_CanResolveAllServices()
    {
        var services = new ServiceCollection();
        services.AddYLproxyTestServices();

        var sp = services.BuildServiceProvider();

        // Verify all registered services can be resolved without throwing
        var exceptions = new List<string>();

        try { sp.GetRequiredService<ConfigurationManager>(); }
        catch (Exception ex) { exceptions.Add($"ConfigurationManager: {ex.Message}"); }

        try { sp.GetRequiredService<ProxyRuntimeConfiguration>(); }
        catch (Exception ex) { exceptions.Add($"ProxyRuntimeConfiguration: {ex.Message}"); }

        try { sp.GetRequiredService<ProxyProcessManager>(); }
        catch (Exception ex) { exceptions.Add($"ProxyProcessManager: {ex.Message}"); }

        try { sp.GetRequiredService<YLproxy.Proxy.Abstractions.IProxyProcessManager>(); }
        catch (Exception ex) { exceptions.Add($"IProxyProcessManager: {ex.Message}"); }

        try { sp.GetRequiredService<IProxyTester>(); }
        catch (Exception ex) { exceptions.Add($"IProxyTester: {ex.Message}"); }

        Assert.Empty(exceptions);
    }

    [Fact]
    public void AddYLproxyFullServices_WithNonexistentConfigFile_ThrowsArgumentExceptionOnResolve()
    {
        // Full service registration requires the config file path to be
        // AppSettings.json at the repository root.
        // A nonexistent config path will throw when IAppSettingsService is resolved.
        var services = new ServiceCollection();
        services.AddYLproxyServices("nonexistent-config.json");

        var sp = services.BuildServiceProvider();
        Assert.Throws<ArgumentException>(() => sp.GetService<IAppSettingsService>());
    }

    [Fact]
    public void Services_AreSingletons()
    {
        var services = new ServiceCollection();
        services.AddYLproxyTestServices();

        var sp = services.BuildServiceProvider();

        var manager1 = sp.GetRequiredService<ConfigurationManager>();
        var manager2 = sp.GetRequiredService<ConfigurationManager>();

        Assert.Same(manager1, manager2);
    }
}

