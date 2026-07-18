using System.Threading;
using System.Threading.Tasks;
using YLproxy.Core.Abstractions;
using YLproxy.Infrastructure;

namespace YLproxy.Core;

/// <summary>
/// Adapter that implements IProxyTester by delegating to the static ProxyTester.
/// Enables DI injection and testability without modifying the existing static class.
/// </summary>
public sealed class ProxyTesterAdapter : IProxyTester
{
    public Task<(bool Success, long LatencyMs, string? Error)> TestAsync(
        string host,
        int port,
        string? username,
        string? password,
        CancellationToken cancellationToken = default)
    {
        return ProxyTester.TestAsync(host, port, username, password, cancellationToken);
    }
}
