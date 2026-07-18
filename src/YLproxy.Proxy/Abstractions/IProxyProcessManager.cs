using YLproxy.Models;

namespace YLproxy.Proxy.Abstractions;

public interface IProxyProcessManager
{
    void Configure(Infrastructure.ThreeProxyConfig settings);

    void Ensure3ProxyDependencies();

    void Start(ProxyItem proxy);

    bool IsRunning(ProxyItem proxy);

    void Stop(ProxyItem proxy);
}
