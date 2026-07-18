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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
