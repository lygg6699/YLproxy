using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows;
using YLproxy.Core;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;
using GlobalConfigService = YLproxy.Infrastructure.AppSettingsService;
using GlobalProxyConfig = YLproxy.Infrastructure.ProxyConfig;
using GlobalThreeProxyConfig = YLproxy.Infrastructure.ThreeProxyConfig;


namespace YLproxy.GUI;

public sealed class MainViewModel : ViewModelBase
{
    private readonly Timer _timer;
    private readonly MonitorService _monitorService;
    private readonly GlobalConfigService _settingsService;
    private readonly ILogger _logger;
    private readonly GlobalProxyConfig _proxyConfig;
    private readonly GlobalThreeProxyConfig _threeProxyConfig;

    private string _computerName = string.Empty;
    private string _ipAddress = "";
    private string _networkStatus = "Unknown";
    private DateTime _now = DateTime.Now;

    public string ComputerName
    {
        get => _computerName;
        set => SetProperty(ref _computerName, value);
    }

    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    public string NetworkStatus
    {
        get => _networkStatus;
        set => SetProperty(ref _networkStatus, value);
    }

    public DateTime Now
    {
        get => _now;
        set => SetProperty(ref _now, value);
    }

    public ObservableCollection<ProxyItem> Proxies { get; } = new();

    private readonly ObservableCollection<string> _logs = new();
    public ObservableCollection<string> Logs => _logs;

    public RelayCommand AddCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand TestCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ClearLogCommand { get; }

    private bool _isTesting;
    public bool IsTesting
    {
        get => _isTesting;
        set => SetProperty(ref _isTesting, value);
    }

    private bool _isStarting;
    public bool IsStarting
    {
        get => _isStarting;
        set => SetProperty(ref _isStarting, value);
    }

    private bool _isStopping;
    public bool IsStopping
    {
        get => _isStopping;
        set => SetProperty(ref _isStopping, value);
    }

    // Dashboard properties
    private int _totalCount;
    public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }

    private int _runningCount;
    public int RunningCount { get => _runningCount; set => SetProperty(ref _runningCount, value); }

    private int _stoppedCount;
    public int StoppedCount { get => _stoppedCount; set => SetProperty(ref _stoppedCount, value); }

    private int _failedCount;
    public int FailedCount { get => _failedCount; set => SetProperty(ref _failedCount, value); }

    private ProxyItem? _selectedProxy;
    public ProxyItem? SelectedProxy
    {
        get => _selectedProxy;
        set => SetProperty(ref _selectedProxy, value);
    }

    public MainViewModel()
    {
        _settingsService = new GlobalConfigService();
        _logger = LoggerFactory.CreateLogger();
        _proxyConfig = _settingsService.GetSection<GlobalProxyConfig>("Proxy");
        _threeProxyConfig = _settingsService.GetSection<GlobalThreeProxyConfig>("ThreeProxy");
        YLproxy.Proxy.ProxyProcessManager.Configure(_threeProxyConfig);

        InitFromConfig();
        LoadHostInfo();
        AddLog($"[{DateTime.Now:HH:mm:ss}] Application started. (Phase 7 with MonitorService) ");

        AddCommand = new RelayCommand(ShowAddWindow);
        RemoveCommand = new RelayCommand(() => OnButtonClicked("Remove"));
        TestCommand = new RelayCommand(() => OnButtonClicked("Test"));
        StartCommand = new RelayCommand(StartSelectedProxy);
        StopCommand = new RelayCommand(StopSelectedProxy);
        ClearLogCommand = new RelayCommand(() => _logs.Clear());

        // Start the background monitor that checks 3proxy process health every 5 seconds
        _monitorService = new MonitorService(
            getProxies: () => Proxies.ToList(),
            logAction: (msg) => AddLog(msg),
            refreshAction: RefreshDataGrid,
            checkInterval: TimeSpan.FromSeconds(Math.Max(1, _proxyConfig.CheckIntervalSeconds)));

        _timer = new Timer(_ => Tick(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Force-refresh DataGrid by re-inserting all items (since ProxyItem lacks INotifyPropertyChanged)
    /// </summary>
    private void RefreshDataGrid()
    {
        // ProxyItem now implements INotifyPropertyChanged, so we don't need to recreate the collection.
        // WPF DataGrid will automatically update when ProxyItem.Status changes.
    }

    private void Tick()
    {
        var now = DateTime.Now;
        var netStatus = GetNetworkStatus();
        var ip = GetBestLocalIp();

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            Now = now;
            NetworkStatus = netStatus;
            if (!string.IsNullOrWhiteSpace(ip)) IpAddress = ip;
        });
    }

    private void InitFromConfig()
    {
        Proxies.Clear();
        try
        {
            var configPath = GetConfigPath();
            var svc = new YLproxy.Core.Config.ProxyDataService(configPath);
            var cfg = svc.Load();

            foreach (var p in cfg.Proxies)
            {
                Proxies.Add(p);
            }

            if (Proxies.Count == 0)
            {
                // If config exists but empty, keep UI stable with an empty list.
                AddLog($"[{DateTime.Now:HH:mm:ss}] config.json loaded: 0 proxies.");
            }
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] config.json could not be loaded: {ex.Message}");
        }
    }

    private string GetConfigPath()
    {
        return PathResolver.ResolvePath(_proxyConfig.DataDirectory, _proxyConfig.ConfigFileName);
    }



    private void OnButtonClicked(string name)
    {
        switch (name)
        {
            case "Remove":
                RemoveSelectedProxyAndPersist();
                break;
            case "Test":
                _ = TestSelectedProxyAsync();
                break;
            default:
                AddLog($"[{DateTime.Now:HH:mm:ss}] Button clicked: {name}");
                break;
        }
    }

    private async Task TestSelectedProxyAsync()
    {
        if (IsTesting) return; // 防止重复点击
        IsTesting = true;
        try
        {
            if (SelectedProxy is null)
            {
                AddLog($"[{DateTime.Now:HH:mm:ss}] Test failed: no proxy selected");
                return;
            }

            var p = SelectedProxy;
            AddLog($"[{DateTime.Now:HH:mm:ss}] Testing proxy: {p.RemoteHost}:{p.RemotePort} ...");

            var result = await YLproxy.Core.ProxyTester.TestAsync(
                p.RemoteHost,
                p.RemotePort,
                string.IsNullOrWhiteSpace(p.Username) ? null : p.Username,
                string.IsNullOrWhiteSpace(p.Password) ? null : p.Password);

            if (result.Success)
            {
                AddLog($"[{DateTime.Now:HH:mm:ss}] 成功：延迟: {result.LatencyMs}ms");
            }
            else
            {
                var err = string.IsNullOrWhiteSpace(result.Error) ? "连接失败" : result.Error;
                AddLog($"[{DateTime.Now:HH:mm:ss}] 失败：{err}");
            }
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Test failed: {ex.Message}");
        }
        finally
        {
            IsTesting = false;
        }
    }

    private void StartSelectedProxy()
    {
        if (IsStarting) return; // 防止重复点击
        IsStarting = true;
        try
        {
            if (SelectedProxy is null)
            {
                AddLog($"[{DateTime.Now:HH:mm:ss}] Start failed: no proxy selected");
                IsStarting = false;
                return;
            }

            var p = SelectedProxy;

            // Prevent UI blocking: start in background thread.
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    p.Status = ProxyStatus.Running;
                    YLproxy.Proxy.ProxyProcessManager.Start(p);
                    AddLog($"[{DateTime.Now:HH:mm:ss}] Started proxy: {p.LocalHost}:{p.LocalPort} -> {p.RemoteHost}:{p.RemotePort}");
                }
                catch (Exception ex)
                {
                    p.Status = ProxyStatus.Failed;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] Start failed: {ex.Message}");
                }
                finally
                {
                    IsStarting = false;
                }
            });
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Start failed: {ex.Message}");
            IsStarting = false;
        }
    }

    private void StopSelectedProxy()
    {
        if (IsStopping) return; // 防止重复点击
        IsStopping = true;
        try
        {
            if (SelectedProxy is null)
            {
                AddLog($"[{DateTime.Now:HH:mm:ss}] Stop failed: no proxy selected");
                IsStopping = false;
                return;
            }

            var p = SelectedProxy;
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    YLproxy.Proxy.ProxyProcessManager.Stop(p);
                    p.Status = ProxyStatus.Stopped;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] Stopped proxy: {p.LocalHost}:{p.LocalPort}");
                }
                catch (Exception ex)
                {
                    p.Status = ProxyStatus.Failed;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] Stop failed: {ex.Message}");
                }
                finally
                {
                    IsStopping = false;
                }
            });
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Stop failed: {ex.Message}");
            IsStopping = false;
        }
    }


    private void ShowAddWindow()

    {
        try
        {
            var configPath = GetConfigPath();
            var vm = new YLproxy.GUI.ViewModels.AddProxyViewModel(
                Proxies.ToList(),
                configPath,
                _proxyConfig.PortRangeStart,
                _proxyConfig.PortRangeEnd);
            vm.CloseAction = () => { /* window will close from code-behind */ };

            var win = new YLproxy.GUI.Views.AddProxyWindow
            {
                Owner = Application.Current?.MainWindow,
                DataContext = vm
            };

            vm.CloseAction = () => win.Dispatcher.BeginInvoke(new Action(() => win.DialogResult = true));

            win.ShowDialog();

            // If confirmed, the ViewModel already persisted to config.
            // Refresh UI list.
            InitFromConfig();
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Add window failed: {ex.Message}");
        }
    }


    private void RemoveSelectedProxyAndPersist()
    {
        try
        {
            if (SelectedProxy is null)
            {
                AddLog($"[{DateTime.Now:HH:mm:ss}] Remove failed: no proxy selected");
                return;
            }

            var id = SelectedProxy.Id;

            // 若正在运行，先停止对应 3proxy，避免孤儿进程
            try
            {
                YLproxy.Proxy.ProxyProcessManager.Stop(SelectedProxy);
            }
            catch
            {
                // ignore
            }

            // 先更新 UI 列表（更直观）
            Proxies.Remove(SelectedProxy);

            var configPath = GetConfigPath();
            var svc = new YLproxy.Core.Config.ProxyDataService(configPath);
            var cfg = svc.Load();

            var before = cfg.Proxies.Count;
            cfg.Proxies.RemoveAll(p => p.Id == id);
            var after = cfg.Proxies.Count;

            svc.Save(cfg);

            AddLog($"[{DateTime.Now:HH:mm:ss}] Remove proxy persisted: configPath={configPath}, RemovedId={id}, ProxiesSaved={after} (before={before})");
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Remove proxy failed: {ex.Message}");
        }
    }


    // Phase 4: hard-coded AddProxyAndPersist removed (now uses AddProxyWindow).



    private void AddLog(string message)
    {
        Application.Current?.Dispatcher.BeginInvoke(() => _logs.Add(message));

        try
        {
            _logger.Info(message);
        }
        catch
        {
            // 文件写入失败不影响 GUI 显示
        }
    }

    private void LoadHostInfo()
    {
        ComputerName = Environment.MachineName;
        NetworkStatus = GetNetworkStatus();
        IpAddress = GetBestLocalIp() ?? "";
        Now = DateTime.Now;

        // LocalHost/LocalPort should come from config when present.
        // If older/empty config is loaded, we keep the values as-is.
    }


    private static string GetNetworkStatus()
    {
        try
        {
            return NetworkInterface.GetIsNetworkAvailable() ? "Connected" : "Disconnected";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string? GetBestLocalIp()
    {
        try
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    return ip.ToString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private void RefreshStats()
    {
        TotalCount = Proxies.Count;
        RunningCount = Proxies.Count(p => p.Status == ProxyStatus.Running);
        StoppedCount = Proxies.Count(p => p.Status == ProxyStatus.Stopped);
        FailedCount = Proxies.Count(p => p.Status == ProxyStatus.Failed);
    }
}


