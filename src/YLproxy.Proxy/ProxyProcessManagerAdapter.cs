using YLproxy.Models;
using YLproxy.Proxy.Abstractions;

namespace YLproxy.Proxy;

/// <summary>
/// Adapter that implements IProxyProcessManager by delegating to a ProxyProcessManager instance.
/// Enables DI injection and testability.
/// </summary>
public sealed class ProxyProcessManagerAdapter : IProxyProcessManager
{
    private readonly ProxyProcessManager _manager;

    public ProxyProcessManagerAdapter()
        : this(new ProxyProcessManager())
    {
    }

    public ProxyProcessManagerAdapter(ProxyProcessManager manager)
    {
        _manager = manager ?? throw new System.ArgumentNullException(nameof(manager));
    }

    public void Configure(Infrastructure.ThreeProxyConfig settings)
    {
        _manager.Configure(settings);
    }

    public void Ensure3ProxyDependencies()
    {
        _manager.Ensure3ProxyDependencies();
    }

    public void Start(ProxyItem proxy)
    {
        _manager.Start(proxy);
    }

    public bool IsRunning(ProxyItem proxy)
    {
        return _manager.IsRunning(proxy);
    }

    public void Stop(ProxyItem proxy)
    {
        _manager.Stop(proxy);
    }
}
