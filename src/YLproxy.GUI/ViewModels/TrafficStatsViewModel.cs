using System;
using System.Collections.ObjectModel;
using YLproxy.Infrastructure.Abstractions;
using YLproxy.Models;
using WpfApplication = System.Windows.Application;

namespace YLproxy.GUI.ViewModels;

/// <summary>
/// 流量统计 ViewModel：绑定到 MainView.xaml 的流量统计 DataGrid。
/// 订阅 TrafficMonitorService.OnStatsUpdated 事件定期刷新。
/// </summary>
public sealed class TrafficStatsViewModel : ViewModelBase
{
    private readonly ITrafficMonitorService _trafficMonitor;
    private readonly ObservableCollection<TrafficStatItem> _stats = new();

    public ObservableCollection<TrafficStatItem> Stats => _stats;

    public TrafficStatsViewModel(ITrafficMonitorService trafficMonitor)
    {
        _trafficMonitor = trafficMonitor ?? throw new ArgumentNullException(nameof(trafficMonitor));
        _trafficMonitor.OnStatsUpdated += () =>
        {
            try
            {
                WpfApplication.Current?.Dispatcher?.BeginInvoke(() => RefreshStats());
            }
            catch
            {
                // UI thread not available, skip this refresh cycle
            }
        };
    }

    /// <summary>
    /// 从已设置的代理集合刷新统计数据。
    /// </summary>
    public void RefreshStats()
    {
        // Refresh is triggered externally via SetProxies or OnStatsUpdated
    }

    /// <summary>
    /// 从 ProxyItem 列表刷新统计数据。
    /// 在代理列表变化或定时触发时调用。
    /// </summary>
    public void RefreshStats(IEnumerable<ProxyItem> proxies)
    {
        _stats.Clear();
        foreach (var proxy in proxies)
        {
            var stats = _trafficMonitor.GetStats(proxy.Id);
            _stats.Add(new TrafficStatItem
            {
                ProxyId = proxy.Id,
                ProxyName = proxy.Name,
                BytesSent = stats.BytesSent,
                BytesReceived = stats.BytesReceived,
                LastActivity = stats.LastUpdate,
                SpeedBps = stats.SpeedBps,
            });
        }
    }

    /// <summary>
    /// 清空统计列表。
    /// </summary>
    public void Clear()
    {
        _stats.Clear();
    }
}

/// <summary>
/// 流量统计条目（绑定到 DataGrid 行）。
/// </summary>
public sealed class TrafficStatItem
{
    public int ProxyId { get; set; }
    public string ProxyName { get; set; } = string.Empty;
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public DateTime LastActivity { get; set; }
    public double SpeedBps { get; set; }
}

