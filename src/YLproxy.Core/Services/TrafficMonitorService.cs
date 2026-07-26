using System;
using System.Collections.Concurrent;
using System.Threading;
using YLproxy.Models;

namespace YLproxy.Core.Services;

/// <summary>
/// 流量监控服务：跟踪每个代理的发送/接收字节数、延迟和最后活动时间。
/// 使用 ConcurrentDictionary 保证线程安全，支持并发读写。
/// </summary>
public sealed class TrafficMonitorService : IDisposable
{
    private readonly ConcurrentDictionary<int, TrafficStats> _stats = new();
    private readonly Timer _updateTimer;
    private bool _disposed;

    public TrafficMonitorService()
    {
        // 每 5 秒触发一次更新回调（可被外部订阅）
        _updateTimer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// 记录一次流量数据。
    /// </summary>
    /// <param name="proxyId">代理 ID</param>
    /// <param name="bytesSent">发送字节数（增量）</param>
    /// <param name="bytesReceived">接收字节数（增量）</param>
    public void RecordTraffic(int proxyId, long bytesSent, long bytesReceived)
    {
        _stats.AddOrUpdate(proxyId,
            id => new TrafficStats
            {
                BytesSent = bytesSent,
                BytesReceived = bytesReceived,
                LastUpdate = DateTime.UtcNow
            },
            (id, existing) =>
            {
                existing.BytesSent += bytesSent;
                existing.BytesReceived += bytesReceived;
                existing.LastUpdate = DateTime.UtcNow;
                return existing;
            });
    }

    /// <summary>
    /// 获取指定代理的流量统计快照。
    /// </summary>
    public TrafficStats GetStats(int proxyId)
    {
        return _stats.TryGetValue(proxyId, out var stats) ? stats : new TrafficStats();
    }

    /// <summary>
    /// 移除指定代理的统计记录（通常在代理停止时调用）。
    /// </summary>
    public void RemoveStats(int proxyId)
    {
        _stats.TryRemove(proxyId, out _);
    }

    /// <summary>
    /// 获取所有代理的统计快照。
    /// </summary>
    public ConcurrentDictionary<int, TrafficStats> GetAllStats() => _stats;

    /// <summary>
    /// 定时更新回调——可用于持久化统计信息或触发 UI 刷新。
    /// 子类或外部可订阅此事件。
    /// </summary>
    public event Action? OnStatsUpdated;

    private void OnTimerTick(object? state)
    {
        try
        {
            OnStatsUpdated?.Invoke();
        }
        catch
        {
            // 防止回调异常导致定时器停止
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _updateTimer?.Dispose();
        _stats.Clear();
    }
}

/// <summary>
/// 单个代理的流量统计数据。
/// </summary>
public sealed class TrafficStats
{
    /// <summary>累计发送字节数</summary>
    public long BytesSent { get; set; }

    /// <summary>累计接收字节数</summary>
    public long BytesReceived { get; set; }

    /// <summary>最后更新 UTC 时间</summary>
    public DateTime LastUpdate { get; set; }

    /// <summary>当前速率（字节/秒）</summary>
    public double SpeedBps { get; set; }
}

