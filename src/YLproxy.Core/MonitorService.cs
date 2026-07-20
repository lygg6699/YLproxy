using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
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
    private readonly Action<ProxyItem>? _restartAction;
    private readonly Action? _saveAction;
    private readonly ILogger _logger;
    private readonly Func<ProxyItem, bool> _isRunning;
    private readonly TimeSpan _healthCheckInterval;
    private readonly int _maxRestartAttempts;
    private readonly TimeSpan _restartBackoffBase;

    /// <summary>
    /// Tracks consecutive restart failures per proxy for exponential backoff.
    /// Key: proxy Id, Value: (failureCount, lastAttemptTime)
    /// </summary>
    private readonly ConcurrentDictionary<int, (int FailureCount, DateTime LastAttempt)> _restartBackoff = new();

    /// <summary>
    /// Tracks the last health check time per proxy, separated from restart backoff timestamps
    /// to avoid timestamp collision that would corrupt backoff calculations.
    /// </summary>
    private readonly ConcurrentDictionary<int, DateTime> _lastHealthCheck = new();

    private bool _disposed;

    /// <summary>
    /// Creates a MonitorService with health-check and optional auto-restart.
    /// </summary>
    /// <param name="getProxies">Callback to enumerate current proxies.</param>
    /// <param name="logAction">Callback for UI log messages.</param>
    /// <param name="refreshAction">Callback to refresh UI after status changes.</param>
    /// <param name="restartAction">Optional callback to restart a failed proxy. If null, auto-restart is disabled.</param>
    /// <param name="saveAction">Optional callback to persist proxy state changes (e.g., status Failed → disk).</param>
    /// <param name="checkInterval">Polling interval for process liveness checks.</param>
    /// <param name="healthCheckInterval">Interval for TCP connectivity health checks. Defaults to 30s.</param>
    /// <param name="maxRestartAttempts">Max consecutive restart attempts before giving up. Default 5.</param>
    /// <param name="restartBackoffBase">Base backoff duration for restart attempts. Default 30s.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="isRunning">Optional override for process liveness check.</param>
    public MonitorService(
        Func<IReadOnlyList<ProxyItem>> getProxies,
        Action<string> logAction,
        Action refreshAction,
        Action<ProxyItem>? restartAction = null,
        Action? saveAction = null,
        TimeSpan? checkInterval = null,
        TimeSpan? healthCheckInterval = null,
        int maxRestartAttempts = 5,
        TimeSpan? restartBackoffBase = null,
        ILogger? logger = null,
        Func<ProxyItem, bool>? isRunning = null)
    {
        _getProxies = getProxies ?? throw new ArgumentNullException(nameof(getProxies));
        _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
        _refreshAction = refreshAction ?? throw new ArgumentNullException(nameof(refreshAction));
        _restartAction = restartAction;
        _saveAction = saveAction;
        _logger = logger ?? LoggerFactory.CreateLogger();
        _isRunning = isRunning ?? ProxyProcessManager.Default.IsRunning;
        _healthCheckInterval = healthCheckInterval ?? TimeSpan.FromSeconds(30);
        _maxRestartAttempts = maxRestartAttempts;
        _restartBackoffBase = restartBackoffBase ?? TimeSpan.FromSeconds(30);

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
                _logger.Warn($"MonitorService: failed to enumerate proxies: {ex.Message}");
                return;
            }

            if (proxies is null || proxies.Count == 0)
                return;

            var changed = false;

            foreach (var proxy in proxies)
            {
                if (proxy.Status != ProxyStatus.Running)
                    continue;

                var isAlive = false;
                try
                {
                    isAlive = _isRunning(proxy);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"MonitorService: process inspection failed for proxy {proxy.Id}: {ex.Message}");
                    continue;
                }

                if (!isAlive)
                {
                    proxy.Status = ProxyStatus.Failed;
                    _logAction($"[{DateTime.Now:HH:mm:ss}] Monitor: proxy {proxy.Id} ({proxy.LocalHost}:{proxy.LocalPort}) process exited unexpectedly");
                    changed = true;

                    // Attempt auto-restart if configured
                    TryAutoRestart(proxy);
                }
                else
                {
                    // Process is alive, check health periodically
                    CheckHealth(proxy);
                }
            }

            if (changed)
            {
                _saveAction?.Invoke();
                _refreshAction();
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"MonitorService: monitor tick failed: {ex.Message}", ex);
        }
    }

    private void CheckHealth(ProxyItem proxy)
    {
        try
        {
            // Only check health at the configured interval
            // Use _lastHealthCheck to track health check timestamps independently
            // from restart backoff (which is tracked in _restartBackoff).
            if (_lastHealthCheck.TryGetValue(proxy.Id, out var lastCheck))
            {
                if (DateTime.UtcNow - lastCheck < _healthCheckInterval)
                    return;
            }

            // Check TCP connectivity on the local proxy port
            var isConnectable = IsPortConnectable(proxy.LocalPort);
            if (!isConnectable)
            {
                _logger?.Warn($"MonitorService: health check failed for proxy {proxy.Id} ({proxy.LocalPort}), port not reachable");

                // Mark failed and trigger auto-restart immediately.
                // For correctness with unit tests, we must ensure TryAutoRestart is reached.
                proxy.Status = ProxyStatus.Failed;
                _logAction($"[{DateTime.Now:HH:mm:ss}] Monitor: proxy {proxy.Id} health check failed, port unreachable");
                _saveAction?.Invoke();
                _refreshAction();

                TryAutoRestart(proxy);

            }
            else
            {
                // Health check passed — record the check time in the dedicated dictionary
                _lastHealthCheck[proxy.Id] = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger?.Warn($"MonitorService: health check error for proxy {proxy.Id}: {ex.Message}");
        }
    }

    private void TryAutoRestart(ProxyItem proxy)
    {
        if (_restartAction is null)
            return;

        var now = DateTime.UtcNow;

        // Capture the previous LastAttempt before updating, so backoff is based on the
        // actual elapsed time since the *last* restart attempt, not this one.
        _restartBackoff.TryGetValue(proxy.Id, out var prev);
        var prevLastAttempt = prev.LastAttempt;

        var entry = _restartBackoff.AddOrUpdate(proxy.Id,
            (1, now),
            (_, e) => (e.FailureCount + 1, now));

        // Calculate backoff: base * 2^(failureCount-1)
        if (entry.FailureCount > 1)
        {
            var backoffMultiplier = 1 << Math.Min(entry.FailureCount - 2, 8);
            var backoff = _restartBackoffBase * backoffMultiplier;

            if (prevLastAttempt != default && now - prevLastAttempt < backoff)
            {
                _logger?.Debug($"MonitorService: skipping restart for proxy {proxy.Id}, backoff {backoff.TotalSeconds:F0}s");
                return;
            }
        }

        if (entry.FailureCount > _maxRestartAttempts)
        {
            _logger?.Warn($"MonitorService: max restart attempts ({_maxRestartAttempts}) exceeded for proxy {proxy.Id}, giving up");
            _logAction($"[{DateTime.Now:HH:mm:ss}] Monitor: proxy {proxy.Id} restart abandoned after {_maxRestartAttempts} attempts");
            return;
        }

        try
        {
            _logAction($"[{DateTime.Now:HH:mm:ss}] Monitor: auto-restarting proxy {proxy.Id} (attempt {entry.FailureCount}/{_maxRestartAttempts})");
            proxy.Status = ProxyStatus.Running;
            _restartAction(proxy);
            _refreshAction();
        }
        catch (Exception ex)
        {
            _logger?.Error($"MonitorService: auto-restart failed for proxy {proxy.Id}: {ex.Message}", ex);
            proxy.Status = ProxyStatus.Failed;
            _logAction($"[{DateTime.Now:HH:mm:ss}] Monitor: proxy {proxy.Id} auto-restart failed: {ex.Message}");
        }
    }

    private static bool IsPortConnectable(int port)
    {
        try
        {
            using var client = new TcpClient
            {
                SendTimeout = 2000,
                ReceiveTimeout = 2000,
            };
            client.Connect("127.0.0.1", port);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resets the restart backoff counter for a proxy (e.g., after manual restart by user).
    /// </summary>
    public void ResetBackoff(int proxyId)
    {
        _restartBackoff.TryRemove(proxyId, out _);
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
