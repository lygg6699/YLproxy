using System.Threading;
using System.Threading.Tasks;

namespace YLproxy.Core.Abstractions;

public interface IProxyTester
{
    Task<(bool Success, long LatencyMs, string? Error)> TestAsync(
        string host,
        int port,
        string? username,
        string? password,
        CancellationToken cancellationToken = default);
}

