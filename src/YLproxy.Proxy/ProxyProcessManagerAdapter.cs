using YLproxy.Models;
using YLproxy.Proxy.Abstractions;

namespace YLproxy.Proxy;

/// <summary>
/// Adapter that implements IProxyProcessManager by delegating to the static ProxyProcessManager.
/// Enables DI injection and testability without modifying the existing static class.
/// </summary>
public sealed class ProxyProcessManagerAdapter : IProxyProcessManager
{
    public void Configure(Infrastructure.ThreeProxyConfig settings)
    {
        ProxyProcessManager.Configure(settings);
    }

    public void Ensure3ProxyDependencies()
    {
        ProxyProcessManager.Ensure3ProxyDependencies();
    }

    public void Start(ProxyItem proxy)
    {
        ProxyProcessManager.Start(proxy);
    }

    public bool IsRunning(ProxyItem proxy)
    {
        return ProxyProcessManager.IsRunning(proxy);
    }

    public void Stop(ProxyItem proxy)
    {
        ProxyProcessManager.Stop(proxy);
    }
}
