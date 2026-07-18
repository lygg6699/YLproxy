using YLproxy.Models;

namespace YLproxy.Core.Abstractions;

// Compatibility shim (optional): not used directly by GUI.
public interface IProxyProcessManager
{
    void Configure(Infrastructure.ThreeProxyConfig settings);

    void Ensure3ProxyDependencies();

    void Start(ProxyItem proxy);

    bool IsRunning(ProxyItem proxy);

    void Stop(ProxyItem proxy);
}
