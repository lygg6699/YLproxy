using System;

namespace YLproxy.Infrastructure.Abstractions;

/// <summary>
/// 流量监控服务抽象接口。
/// 允许 Proxy / Core 项目解耦引用具体实现。
/// </summary>
public interface ITrafficMonitorService : IDisposable
{
    /// <summary>
    /// 记录流量数据（增量累计）。
    /// </summary>
    void RecordTraffic(int proxyId, long bytesSent, long bytesReceived);

    /// <summary>
    /// 获取指定代理的流量统计快照。
    /// </summary>
    TrafficStats GetStats(int proxyId);

    /// <summary>
    /// 移除指定代理的统计记录。
    /// </summary>
    void RemoveStats(int proxyId);

    /// <summary>
    /// 定时更新事件——可用于触发 UI 刷新。
    /// </summary>
    event Action? OnStatsUpdated;
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

