using System;
using System.Collections.Generic;
using System.Threading;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Proxy;

namespace YLproxy.Core;

public sealed class MonitorService : IDisposable
{
    private readonly Timer _timer;
    private readonly Func<IReadOnlyList<ProxyItem>> _getProxies;
    private readonly Action<string> _logAction;
    private readonly Action _refreshAction;
    private readonly ILogger _logger;
    private bool _disposed;

    public MonitorService(
        Func<IReadOnlyList<ProxyItem>> getProxies,
        Action<string> logAction,
        Action refreshAction,
        TimeSpan? checkInterval = null)
    {
        _getProxies = getProxies ?? throw new ArgumentNullException(nameof(getProxies));
        _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
        _refreshAction = refreshAction ?? throw new ArgumentNullException(nameof(refreshAction));
        _logger = LoggerFactory.CreateLogger();
        var interval = checkInterval ?? TimeSpan.FromSeconds(5);
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(checkInterval));

        _timer = new Timer(MonitorTick, null, interval, interval);
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
            catch (Exception ex)
            {
                _logger.Warn($"Monitor: failed to retrieve proxy list: {ex.Message}");
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
                catch (Exception ex)
                {
                    _logger.Warn($"Monitor: process check failed for proxy {proxy.Id}: {ex.Message}");
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
        catch (Exception ex)
        {
            _logger.Error($"Monitor: unexpected error in monitor tick: {ex.Message}", ex);
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