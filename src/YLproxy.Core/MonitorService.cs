using System;
using System.Collections.Generic;
using System.Threading;
using YLproxy.Models;
using YLproxy.Proxy;

namespace YLproxy.Core;

public sealed class MonitorService : IDisposable
{
    private readonly Timer _timer;
    private readonly Func<IReadOnlyList<ProxyItem>> _getProxies;
    private readonly Action<string> _logAction;
    private readonly Action _refreshAction;
    private bool _disposed;

    public MonitorService(
        Func<IReadOnlyList<ProxyItem>> getProxies,
        Action<string> logAction,
        Action refreshAction)
    {
        _getProxies = getProxies ?? throw new ArgumentNullException(nameof(getProxies));
        _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
        _refreshAction = refreshAction ?? throw new ArgumentNullException(nameof(refreshAction));
        _timer = new Timer(MonitorTick, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private void MonitorTick(object? state)
    {
        try
        {
            IReadOnlyList<ProxyItem> proxies;

            try
            {
                proxies = _getProxies();
            }
            catch
            {
                return;
            }

            if (proxies is null || proxies.Count == 0)
                return;

            var changed = false;

            foreach (var proxy in proxies)
            {
                // Only monitor proxies that should be running
                if (proxy.Status != ProxyStatus.Running)
                    continue;

                var isAlive = false;
                try
                {
                    isAlive = ProxyProcessManager.IsRunning(proxy);
                }
                catch
                {
                    // Process tracking issue - treat as dead
                }

                if (!isAlive)
                {
                    proxy.Status = ProxyStatus.Failed;
                    _logAction($"[{DateTime.Now:HH:mm:ss}] Monitor: proxy {proxy.Id} ({proxy.LocalHost}:{proxy.LocalPort}) process exited unexpectedly");
                    changed = true;
                }
            }

            if (changed)
            {
                _refreshAction();
            }
        }
        catch
        {
            // Silently handle monitor exceptions to avoid crashing the timer
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _timer?.Dispose();
        }
    }
}