using System;
using YLproxy.GUI;

namespace YLproxy.GUI.ViewModels;

public sealed class HostInfoViewModel : ViewModelBase
{
    private string _computerName = string.Empty;
    public string ComputerName
    {
        get => _computerName;
        set => SetProperty(ref _computerName, value);
    }

    private string _ipAddress = string.Empty;
    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    private string _networkStatus = "Unknown";
    public string NetworkStatus
    {
        get => _networkStatus;
        set => SetProperty(ref _networkStatus, value);
    }

    private DateTime _now;
    public DateTime Now
    {
        get => _now;
        set => SetProperty(ref _now, value);
    }
}

