using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YLproxy.Models;

public sealed class ProxyItem : INotifyPropertyChanged
{
    private ProxyStatus _status = ProxyStatus.Stopped;

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public string RemoteHost { get; set; } = string.Empty;
    public int RemotePort { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string LocalHost { get; set; } = string.Empty;
    public int LocalPort { get; set; }

    public ProxyStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime CreateTime { get; init; } = DateTime.UtcNow;

    // ================================================================
    // 流量统计字段 (Phase 5.1)
    // ================================================================
    /// <summary>
    /// 累计发送字节数（从代理启动至今）
    /// </summary>
    public long TotalBytesSent { get; set; }

    /// <summary>
    /// 累计接收字节数（从代理启动至今）
    /// </summary>
    public long TotalBytesReceived { get; set; }

    /// <summary>
    /// 最后活动时间（UTC）
    /// </summary>
    public DateTime LastActivityTime { get; set; }

    /// <summary>
    /// 平均延迟（毫秒）
    /// </summary>
    public double AverageLatency { get; set; }

    /// <summary>
    /// 当前会话流量速率（字节/秒），仅用于 UI 实时显示
    /// </summary>
    public double CurrentSpeedBps { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
