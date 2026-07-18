using System;

namespace YLproxy.GUI.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private int _totalCount;
    public int TotalCount
    {
        get => _totalCount;
        set => SetProperty(ref _totalCount, value);
    }

    private int _runningCount;
    public int RunningCount
    {
        get => _runningCount;
        set => SetProperty(ref _runningCount, value);
    }

    private int _stoppedCount;
    public int StoppedCount
    {
        get => _stoppedCount;
        set => SetProperty(ref _stoppedCount, value);
    }

    private int _failedCount;
    public int FailedCount
    {
        get => _failedCount;
        set => SetProperty(ref _failedCount, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
}

