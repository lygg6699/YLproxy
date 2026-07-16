using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YLproxy.Models;

public sealed class ProxyItem : INotifyPropertyChanged
{
    private ProxyStatus _status = ProxyStatus.Stopped;

    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;

    public string RemoteHost { get; init; } = string.Empty;
    public int RemotePort { get; init; }

    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public string LocalHost { get; init; } = string.Empty;
    public int LocalPort { get; init; }

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
