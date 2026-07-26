using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using YLproxy.Api;
using YLproxy.Core;
using YLproxy.GUI.ViewModels;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Models.Config;
using YLproxy.Utils;
using GlobalConfigService = YLproxy.Infrastructure.AppSettingsService;
using GlobalProxyConfig = YLproxy.Models.Config.ProxyConfig;
using GlobalThreeProxyConfig = YLproxy.Models.Config.ThreeProxyConfig;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Threading.Timer;

// MainViewModel refactored to use sub-ViewModels
namespace YLproxy.GUI;

public sealed class MainViewModel : ViewModelBase
{
    private readonly Timer _timer;
    private readonly MonitorService _monitorService;
    private readonly GlobalConfigService _settingsService;
    private readonly ILogger _logger;
    private readonly GlobalProxyConfig _proxyConfig;
    private readonly GlobalThreeProxyConfig _threeProxyConfig;

    private readonly Core.Abstractions.IProxyDataService _proxyDataService;
    private readonly Core.Abstractions.IProxyTester _proxyTester;
    private readonly Proxy.Abstractions.IProxyProcessManager _proxyProcessManager;
    private readonly ApiServer _apiServer;



    // --- Sub-ViewModels ---
    public HostInfoViewModel HostInfo { get; } = new();
    public DashboardViewModel Dashboard { get; } = new();
    public LogPanelViewModel LogPanel { get; } = new();
    public ProxyListViewModel ProxyList { get; }
    public ProxyOperationViewModel ProxyOperations { get; }
    public ImportExportViewModel ImportExport { get; }

    // --- Proxy Collections ---
    public ObservableCollection<ProxyItem> Proxies => ProxyList.Proxies;
    public ObservableCollection<ProxyItem> FilteredProxies => ProxyList.FilteredProxies;

    public string SearchText
    {
        get => ProxyList.SearchText;
        set => ProxyList.SearchText = value;
    }

    public List<ProxyItem> SelectedProxies { get; set; } = new();

    // --- Commands ---
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand TestCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand BatchStartCommand { get; }
    public RelayCommand BatchStopCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand ClearSearchCommand { get; }

    // --- Search / Filter ---

    // --- Status Message ---
    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // --- Operation Guards ---
    private bool _isTesting;
    public bool IsTesting { get => _isTesting; set => SetProperty(ref _isTesting, value); }

    private bool _isStarting;
    public bool IsStarting { get => _isStarting; set => SetProperty(ref _isStarting, value); }

    private bool _isStopping;
    public bool IsStopping { get => _isStopping; set => SetProperty(ref _isStopping, value); }

    private bool _isExporting;
    public bool IsExporting { get => _isExporting; set => SetProperty(ref _isExporting, value); }

    private bool _isImporting;
    public bool IsImporting { get => _isImporting; set => SetProperty(ref _isImporting, value); }

    // --- API 状态 ---
    private string _apiStatus = "Stopped";
    public string ApiStatus
    {
        get => _apiStatus;
        set => SetProperty(ref _apiStatus, value);
    }

    private int _apiPort;
    public int ApiPort
    {
        get => _apiPort;
        set => SetProperty(ref _apiPort, value);
    }

    private ProxyItem? _selectedProxy;
    public ProxyItem? SelectedProxy
    {
        get => _selectedProxy;
        set => SetProperty(ref _selectedProxy, value);
    }

    // ================================================================
    public MainViewModel(
        ILogger logger,
        GlobalConfigService settingsService,
        GlobalProxyConfig proxyConfig,
        GlobalThreeProxyConfig threeProxyConfig,
        Core.Abstractions.IProxyDataService proxyDataService,
        Core.Abstractions.IProxyTester proxyTester,
        Proxy.Abstractions.IProxyProcessManager proxyProcessManager,
        ApiServer apiServer)
    {
        _logger = logger;
        _settingsService = settingsService;
        _proxyConfig = proxyConfig;
        _threeProxyConfig = threeProxyConfig;
        _proxyDataService = proxyDataService;
        _proxyTester = proxyTester;
        _proxyProcessManager = proxyProcessManager;
        _apiServer = apiServer;
        _proxyProcessManager.Configure(_threeProxyConfig);

        // 初始化 API 状态
        _apiPort = apiServer.Port;
        _apiStatus = apiServer.IsRunning ? "Running" : "Stopped";
        Dashboard.UpdateApiStatus(_apiStatus, _apiPort);

        InitFromConfig();
        LoadHostInfo();
        RefreshStats();
        AddLog($"[{DateTime.Now:HH:mm:ss}] Application started. (Phase 8 — GUI Enhanced)");

        AddCommand = new RelayCommand(ShowAddWindow);

        EditCommand = new RelayCommand(ShowEditWindow, () => SelectedProxy is not null);
        RemoveCommand = new RelayCommand(RemoveSelectedProxyAndPersist, () => SelectedProxy is not null);
        TestCommand = new RelayCommand(() => _ = TestSelectedProxyAsync(), () => SelectedProxy is not null);
        StartCommand = new RelayCommand(StartSelectedProxy, () => SelectedProxy is not null);
        StopCommand = new RelayCommand(StopSelectedProxy, () => SelectedProxy is not null);
        ClearLogCommand = new RelayCommand(() => LogPanel.ClearLogCommand.Execute(null));
        BatchStartCommand = new RelayCommand(BatchStart, () => SelectedProxies.Count > 0);
        BatchStopCommand = new RelayCommand(BatchStop, () => SelectedProxies.Count > 0);
        ExportCommand = new RelayCommand(ExportToJson);
        ImportCommand = new RelayCommand(ImportFromJson);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        // Initialize sub-ViewModels
        ProxyList = new ProxyListViewModel();
        ProxyOperations = new ProxyOperationViewModel(proxyTester, proxyProcessManager, _logger);
        ImportExport = new ImportExportViewModel(_logger);

        _monitorService = new MonitorService(
            getProxies: () => Proxies.ToList(),
            logAction: (msg) => AddLog(msg),
            refreshAction: RefreshDataGrid,
            restartAction: RestartProxySafe,
            saveAction: PersistProxyState,
            checkInterval: TimeSpan.FromSeconds(Math.Max(1, _proxyConfig.CheckIntervalSeconds)),
            logger: _logger);

        _timer = new Timer(_ => Tick(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    // ================================================================
    // Filtering
    // ================================================================
    private void ApplyProxyFilter()
    {
        ProxyList.RefreshFilter();
    }

    // ================================================================
    // Ticking
    // ================================================================
    private int _proxiesVersion;

    private void RefreshDataGrid()
    {
        Interlocked.Increment(ref _proxiesVersion);
        // Force re-evaluation of filter + notify UI to refresh DataGrid
        var current = SearchText;
        SearchText = current; // trigger ApplyProxyFilter via the property setter
        RaisePropertyChanged(nameof(FilteredProxies));
        RaisePropertyChanged(nameof(Proxies));
    }

    private void Tick()
    {
        var now = DateTime.Now;
        var netStatus = GetNetworkStatus();
        var ip = YLproxy.Utils.NetworkUtil.GetBestLocalIp();

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            HostInfo.Now = now;
            HostInfo.NetworkStatus = netStatus;
            if (!string.IsNullOrWhiteSpace(ip)) HostInfo.IpAddress = ip;
        });
    }

    // ================================================================
    // Init & Config
    // ================================================================
    private void InitFromConfig()
    {
        ProxyList.Proxies.Clear();
        ProxyList.FilteredProxies.Clear();
        try
        {
            var cfg = _proxyDataService.Load();

            foreach (var p in cfg.Proxies)
                ProxyList.Proxies.Add(p);

            ApplyProxyFilter();

            if (Proxies.Count == 0)
                AddLog($"[{DateTime.Now:HH:mm:ss}] config.json loaded: 0 proxies.");
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

    // ================================================================
    // Add / Edit Windows
    // ================================================================
    private void ShowAddWindow()
    {
        try
        {
            var configPath = GetConfigPath();
            var vm = new AddProxyViewModel(
                Proxies.ToList(), configPath,
                _proxyConfig.PortRangeStart, _proxyConfig.PortRangeEnd);

            vm.CloseAction = () => { };
            var win = new Views.AddProxyWindow
            {
                Owner = Application.Current?.MainWindow,
                DataContext = vm
            };
            vm.CloseAction = () => win.Dispatcher.BeginInvoke(new Action(() => win.DialogResult = true));
            win.ShowDialog();

            if (win.DialogResult == true)
            {
                InitFromConfig();
                RefreshStats();
            }
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Add window failed: {ex.Message}");
        }
    }

    private void ShowEditWindow()
    {
        var proxy = SelectedProxy;
        if (proxy is null) return;

        if (proxy.Status == ProxyStatus.Running)
        {
            SetStatus("Cannot edit a running proxy. Stop it first.");
            return;
        }

        try
        {
            var configPath = GetConfigPath();
            var vm = new AddProxyViewModel(
                Proxies.ToList(), configPath,
                _proxyConfig.PortRangeStart, _proxyConfig.PortRangeEnd,
                editTarget: proxy);

            vm.CloseAction = () => { };
            var win = new Views.AddProxyWindow
            {
                Owner = Application.Current?.MainWindow,
                DataContext = vm,
                Title = "编辑代理"
            };
            vm.CloseAction = () => win.Dispatcher.BeginInvoke(new Action(() => win.DialogResult = true));
            win.ShowDialog();

            if (win.DialogResult == true)
            {
                InitFromConfig();
                RefreshStats();
            }
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Edit window failed: {ex.Message}");
        }
    }

    // ================================================================
    // Remove
    // ================================================================
    private void RemoveSelectedProxyAndPersist()
    {
        try
        {
            if (SelectedProxy is null)
            {
                SetStatus("Remove failed: no proxy selected");
                return;
            }

            var proxy = SelectedProxy;

            var result = MessageBox.Show(
                $"确定要删除代理「{proxy.Name}」(ID: {proxy.Id}) 吗？",
                "YLproxy — 删除确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // Attempt to stop the proxy, but continue with removal even if stopping fails
            try { _proxyProcessManager.Stop(proxy); }
            catch (Exception ex)
            {
                _logger.Warn($"Stop proxy {proxy.Id} before removal failed (non-critical): {ex.Message}");
            }

            Proxies.Remove(proxy);
            ApplyProxyFilter();

            var cfg = _proxyDataService.Load();
            cfg.Proxies.RemoveAll(p => p.Id == proxy.Id);
            _proxyDataService.Save(cfg);

            RefreshStats();
            SetStatus($"Deleted: {proxy.Name}");
            AddLog($"[{DateTime.Now:HH:mm:ss}] Removed: {proxy.Name} (ID:{proxy.Id})");
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Remove failed: {ex.Message}");
        }
    }

    // ================================================================
    // Test
    // ================================================================
    private async Task TestSelectedProxyAsync()
    {
        if (SelectedProxy is not null)
        {
            await ProxyOperations.TestSelectedProxyAsync(SelectedProxy);
        }
    }

    // ================================================================
    // Start / Stop (single)
    // ================================================================
    private void StartSelectedProxy()
    {
        if (SelectedProxy is not null)
        {
            ProxyOperations.StartSelectedProxy(SelectedProxy);
        }
    }

    private void StopSelectedProxy()
    {
        if (SelectedProxy is not null)
        {
            ProxyOperations.StopSelectedProxy(SelectedProxy);
        }
    }

    // ================================================================
    // Batch Operations
    // ================================================================
    private void BatchStart()
    {
        if (SelectedProxies.Count > 0)
        {
            ProxyOperations.BatchStart(SelectedProxies.ToList());
        }
    }

    private void BatchStop()
    {
        if (SelectedProxies.Count > 0)
        {
            ProxyOperations.BatchStop(SelectedProxies.ToList());
        }
    }

    // ================================================================
    // Import / Export
    // ================================================================
    private void ExportToJson()
    {
        var exportProxies = SelectedProxies.Count > 0
            ? SelectedProxies
            : Proxies.ToList();
        ImportExport.ExportToJson(exportProxies);
    }

    private void ImportFromJson()
    {
        ImportExport.ImportFromJson(Proxies.ToList());
        InitFromConfig();
        RefreshStats();
    }

    // ================================================================
    // Restart (MonitorService callback)
    // ================================================================
    private void RestartProxySafe(ProxyItem proxy)
    {
        _ = Task.Run(() =>
        {
            try
            {
                _proxyProcessManager.Stop(proxy);
                Thread.Sleep(500);
                _proxyProcessManager.Start(proxy);
            }
            catch (Exception ex)
            {
                proxy.Status = ProxyStatus.Failed;
                AddLog($"[{DateTime.Now:HH:mm:ss}] Monitor: auto-restart proxy {proxy.Id} failed: {ex.Message}");
            }
        });
    }

    // ================================================================
    // Logging (LogEntry-based)
    // ================================================================
    private void AddLog(string message)
    {
        var entry = LogEntry.FromRawString(message);
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            LogPanel.AddRawLog(message);
        });

        try { _logger.Info(message); }
        catch (Exception ex)
        {
            // Logging failure is non-critical; swallow to avoid crashing the application.
            System.Diagnostics.Debug.WriteLine($"AddLog: failed to write log entry: {message}");
            _logger.Warn($"AddLog: failed to write log entry: {ex.Message}");
        }
        // Ignore logging failures to prevent logging issues from crashing the application
    }

    private void SetStatus(string message)
    {
        Application.Current?.Dispatcher.BeginInvoke(() => StatusMessage = message);
    }

    // ================================================================
    // Host Info
    // ================================================================
    private void LoadHostInfo()
    {
        HostInfo.ComputerName = Environment.MachineName;
        HostInfo.NetworkStatus = GetNetworkStatus();
        HostInfo.IpAddress = YLproxy.Utils.NetworkUtil.GetBestLocalIp() ?? "";
        HostInfo.Now = DateTime.Now;
    }

    private static string GetNetworkStatus()
    {
        try
        {
            return NetworkInterface.GetIsNetworkAvailable() ? "Connected" : "Disconnected";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetNetworkStatus: failed to check network: {ex.Message}");
            return "Unknown";
        }
    }

    public async Task ShutdownAsync()
    {
        // Attempt to stop each proxy, but continue shutdown even if stopping fails
        foreach (var proxy in Proxies.Where(p => p.Status == ProxyStatus.Running).ToList())
        {
            try { _proxyProcessManager.Stop(proxy); }
            catch (Exception ex)
            {
                _logger.Warn($"Stop proxy {proxy.Id} before removal failed (non-critical): {ex.Message}");
            }
        }

        // 停止 API 服务器
        try
        {
            if (_apiServer.IsRunning)
            {
                await _apiServer.StopAsync();
                ApiStatus = "Stopped";
                Dashboard.UpdateApiStatus("Stopped", _apiPort);
                _logger.Info("API server stopped during shutdown.");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to stop API server during shutdown: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 返回 ApiServer 实例，供 App.OnExit 在关闭时使用。
    /// </summary>
    public ApiServer GetApiServer() => _apiServer;

    private void RefreshStats()
    {
        Dashboard.TotalCount = Proxies.Count;
        Dashboard.RunningCount = Proxies.Count(p => p.Status == ProxyStatus.Running);
        Dashboard.StoppedCount = Proxies.Count(p => p.Status == ProxyStatus.Stopped);
        Dashboard.FailedCount = Proxies.Count(p => p.Status == ProxyStatus.Failed);
    }

    private void PersistProxyState()
    {
        try
        {
            var cfg = _proxyDataService.Load();
            var proxyList = Proxies.ToList();
            foreach (var p in cfg.Proxies)
            {
                var live = proxyList.FirstOrDefault(x => x.Id == p.Id);
                if (live is not null)
                    p.Status = live.Status;
            }
            _proxyDataService.Save(cfg);
        }
        catch (Exception ex)
        {
            _logger.Warn($"PersistProxyState: failed to save config.json: {ex.Message}");
        }
    }
}
