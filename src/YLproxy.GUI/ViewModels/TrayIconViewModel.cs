using System.Windows.Input;
using System.Windows.Media;
using Hardcodet.Wpf.TaskbarNotification;
using Application = System.Windows.Application;
using WindowState = System.Windows.WindowState;

namespace YLproxy.GUI.ViewModels;

public sealed class TrayIconViewModel : ViewModelBase
{
    private readonly TaskbarIcon _trayIcon;
    private readonly MainViewModel _mainViewModel;
    private bool _isMinimized;

    public bool IsMinimized
    {
        get => _isMinimized;
        set => SetProperty(ref _isMinimized, value);
    }

    public ICommand ShowWindowCommand { get; }
    public ICommand HideWindowCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand StartAllCommand { get; }
    public ICommand StopAllCommand { get; }

    public TrayIconViewModel(TaskbarIcon trayIcon, MainViewModel mainViewModel)
    {
        _trayIcon = trayIcon;
        _mainViewModel = mainViewModel;

        ShowWindowCommand = new RelayCommand(ShowWindow);
        HideWindowCommand = new RelayCommand(HideWindow);
        ExitCommand = new RelayCommand(Exit);
        StartAllCommand = new RelayCommand(StartAll);
        StopAllCommand = new RelayCommand(StopAll);
    }

    private void ShowWindow()
    {
        var window = Application.Current.MainWindow;
        if (window != null)
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
            IsMinimized = false;
        }
    }

    private void HideWindow()
    {
        var window = Application.Current.MainWindow;
        if (window != null)
        {
            window.Hide();
            IsMinimized = true;
        }
    }

    private void Exit()
    {
        Application.Current.Shutdown();
    }

    private void StartAll()
    {
        _mainViewModel?.BatchStartCommand.Execute(null);
    }

    private void StopAll()
    {
        _mainViewModel?.BatchStopCommand.Execute(null);
    }

    /// <summary>
    /// 更新托盘图标的工具提示文本和图标
    /// </summary>
    public void UpdateStatus(int runningCount, int totalCount)
    {
        var status = runningCount > 0 ? $"运行中: {runningCount}/{totalCount}" : "已停止";
        _trayIcon.ToolTipText = $"YLproxy - {status}";

        // 根据运行状态切换图标
        if (runningCount > 0)
        {
            _trayIcon.IconSource = new ImageSourceConverter().ConvertFromString("pack://application:,,,/Assets/app-running.ico") as ImageSource;
        }
        else
        {
            _trayIcon.IconSource = new ImageSourceConverter().ConvertFromString("pack://application:,,,/Assets/app.ico") as ImageSource;
        }
    }
}
