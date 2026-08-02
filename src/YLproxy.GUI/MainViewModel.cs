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
using YLproxy.GUI.Services;
using YLproxy.Infrastructure;
using YLproxy.Infrastructure.Abstractions;
using YLproxy.Models;
using YLproxy.Models.Config;
using YLproxy.Utils;
using GlobalConfigService = YLproxy.Infrastructure.AppSettingsService;
using GlobalProxyConfig = YLproxy.Models.Config.ProxyConfig;
using GlobalThreeProxyConfig = YLproxy.Models.Config.ThreeProxyConfig;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Threading.Timer;

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
    public ViewModels.TrayIconViewModel? TrayIcon { get; set; }
    public GroupViewModel Groups { get; } = new();
    public TrafficStatsViewModel TrafficStats { get; }

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
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand ManageGroupsCommand { get; }
    public RelayCommand StartGroupCommand { get; }
    public RelayCommand StopGroupCommand { get; }

    // --- Status Message ---
    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // --- API 状态 ---
    private string _apiStatus = "Stopped";
    public string ApiStatus { get => _apiStatus; set => SetProperty(ref _apiStatus, value); }
    private int _apiPort;
    public int ApiPort { get => _apiPort; set => SetProperty(ref _apiPort, value); }

    private ProxyItem? _selectedProxy;
    public ProxyItem? SelectedProxy
    {
        get => _selectedProxy;
        set => SetProperty(ref _selectedProxy, value);
    }

    // --- Theme ---
    private bool _isDarkTheme = true;
    public bool IsDarkTheme { get => _isDarkTheme; set => SetProperty(ref _isDarkTheme, value); }

    // ================================================================
    public MainViewModel(
        ILogger logger,
        GlobalConfigService settingsService,
        GlobalProxyConfig proxyConfig,
        GlobalThreeProxyConfig threeProxyConfig,
        Core.Abstractions.IProxyDataService proxyDataService,
        Core.Abstractions.IProxyTester proxyTester,
        Proxy.Abstractions.IProxyProcessManager proxyProcessManager,
        ApiServer apiServer,
        ITrafficMonitorService trafficMonitorService)
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

        TrafficStats = new TrafficStatsViewModel(trafficMonitorService);

        _apiPort = apiServer.Port;
        _apiStatus = apiServer.IsRunning ? "Running" : "Stopped";
        Dashboard.UpdateApiStatus(_apiStatus, _apiPort);

        ProxyList = new ProxyListViewModel();
        ProxyOperations = new ProxyOperationViewModel(proxyTester, proxyProcessManager, _logger);
        ImportExport = new ImportExportViewModel(_logger);

        InitFromConfig();
        TrafficStats.SetProxies(ProxyList.Proxies);
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
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ManageGroupsCommand = new RelayCommand(ShowManageGroupsWindow);
        StartGroupCommand = new RelayCommand(StartGroupProxies, () => !string.IsNullOrEmpty(Groups.SelectedGroup) && Groups.SelectedGroup != "全部");
        StopGroupCommand = new RelayCommand(StopGroupProxies, () => !string.IsNullOrEmpty(Groups.SelectedGroup) && Groups.SelectedGroup != "全部");

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
    private void ApplyProxyFilter() { ProxyList.RefreshFilter(); }

    private int _proxiesVersion;
    private void RefreshDataGrid()
    {
        Interlocked.Increment(ref _proxiesVersion);
        var current = SearchText;
        SearchText = current;
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
    private void InitFromConfig()
    {
        ProxyList.Proxies.Clear();
        ProxyList.FilteredProxies.Clear();
        try
        {
            var cfg = _proxyDataService.Load();
            foreach (var p in cfg.Proxies) ProxyList.Proxies.Add(p);
            ApplyProxyFilter();
            Groups.LoadGroupsFromProxies(ProxyList.Proxies);
        }
        catch (Exception ex) { AddLog($"[{DateTime.Now:HH:mm:ss}] config.json could not be loaded: {ex.Message}"); }
    }

    private string GetConfigPath() => PathResolver.ResolvePath(_proxyConfig.DataDirectory, _proxyConfig.ConfigFileName);

    // ================================================================
    private void ShowAddWindow()
    {
        try
        {
            var configPath = GetConfigPath();
            var vm = new AddProxyViewModel(Proxies.ToList(), configPath, _proxyConfig.PortRangeStart, _proxyConfig.PortRangeEnd);
            vm.CloseAction = () => { };
            var win = new Views.AddProxyWindow { Owner = Application.Current?.MainWindow, DataContext = vm };
            vm.CloseAction = () => win.Dispatcher.BeginInvoke(new Action(() => win.DialogResult = true));
            win.ShowDialog();
            if (win.DialogResult == true) { InitFromConfig(); RefreshStats(); }
        }
        catch (Exception ex) { AddLog($"[{DateTime.Now:HH:mm:ss}] Add window failed: {ex.Message}"); }
    }

    private void ShowEditWindow()
    {
        var proxy = SelectedProxy;
        if (proxy is null) return;
        if (proxy.Status == ProxyStatus.Running) { SetStatus("Cannot edit a running proxy. Stop it first."); return; }
        try
        {
            var configPath = GetConfigPath();
            var vm = new AddProxyViewModel(Proxies.ToList(), configPath, _proxyConfig.PortRangeStart, _proxyConfig.PortRangeEnd, editTarget: proxy);
            vm.CloseAction = () => { };
            var win = new Views.AddProxyWindow { Owner = Application.Current?.MainWindow, DataContext = vm, Title = "编辑代理" };
            vm.CloseAction = () => win.Dispatcher.BeginInvoke(new Action(() => win.DialogResult = true));
            win.ShowDialog();
            if (win.DialogResult == true) { InitFromConfig(); RefreshStats(); }
        }
        catch (Exception ex) { AddLog($"[{DateTime.Now:HH:mm:ss}] Edit window failed: {ex.Message}"); }
    }

    private void RemoveSelectedProxyAndPersist()
    {
        if (SelectedProxy is null) { SetStatus("Remove failed: no proxy selected"); return; }
        var proxy = SelectedProxy;
        var result = MessageBox.Show($"确定要删除代理「{proxy.Name}」(ID: {proxy.Id}) 吗？", "YLproxy — 删除确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        try { _proxyProcessManager.Stop(proxy); } catch (Exception ex) { _logger.Warn($"Stop proxy {proxy.Id} before removal failed: {ex.Message}"); }
        Proxies.Remove(proxy); ApplyProxyFilter();
        var cfg = _proxyDataService.Load(); cfg.Proxies.RemoveAll(p => p.Id == proxy.Id); _proxyDataService.Save(cfg);
        RefreshStats(); SetStatus($"Deleted: {proxy.Name}"); AddLog($"[{DateTime.Now:HH:mm:ss}] Removed: {proxy.Name} (ID:{proxy.Id})");
    }

    private async Task TestSelectedProxyAsync()
    {
        if (SelectedProxy is not null) { await ProxyOperations.TestSelectedProxyAsync(SelectedProxy); RefreshStats(); }
    }

    private void StartSelectedProxy() { if (SelectedProxy is not null) { ProxyOperations.StartSelectedProxy(SelectedProxy); RefreshStats(); } }
    private void StopSelectedProxy() { if (SelectedProxy is not null) { ProxyOperations.StopSelectedProxy(SelectedProxy); RefreshStats(); } }
    private void BatchStart() { if (SelectedProxies.Count > 0) { ProxyOperations.BatchStart(SelectedProxies.ToList()); RefreshStats(); } }
    private void BatchStop() { if (SelectedProxies.Count > 0) { ProxyOperations.BatchStop(SelectedProxies.ToList()); RefreshStats(); } }

    private void ExportToJson() { var list = SelectedProxies.Count > 0 ? SelectedProxies : Proxies.ToList(); ImportExport.ExportToJson(list); }
    private void ImportFromJson() { ImportExport.ImportFromJson(Proxies.ToList()); InitFromConfig(); RefreshStats(); }

    private void RestartProxySafe(ProxyItem proxy)
    {
        _ = Task.Run(() =>
        {
            try { _proxyProcessManager.Stop(proxy); Thread.Sleep(500); _proxyProcessManager.Start(proxy); }
            catch (Exception ex) { proxy.Status = ProxyStatus.Failed; AddLog($"[{DateTime.Now:HH:mm:ss}] Monitor: auto-restart proxy {proxy.Id} failed: {ex.Message}"); }
        });
    }

    private void AddLog(string message)
    {
        Application.Current?.Dispatcher.BeginInvoke(() => LogPanel.AddRawLog(message));
        try { _logger.Info(message); } catch { }
    }

    private void SetStatus(string message) { Application.Current?.Dispatcher.BeginInvoke(() => StatusMessage = message); }

    private void LoadHostInfo()
    {
        HostInfo.ComputerName = Environment.MachineName;
        HostInfo.NetworkStatus = GetNetworkStatus();
        HostInfo.IpAddress = YLproxy.Utils.NetworkUtil.GetBestLocalIp() ?? "";
        HostInfo.Now = DateTime.Now;
    }

    private static string GetNetworkStatus()
    {
        try { return NetworkInterface.GetIsNetworkAvailable() ? "Connected" : "Disconnected"; }
        catch { return "Unknown"; }
    }

    public async Task ShutdownAsync()
    {
        foreach (var proxy in Proxies.Where(p => p.Status == ProxyStatus.Running).ToList())
        { try { _proxyProcessManager.Stop(proxy); } catch { } }
        try
        {
            if (_apiServer.IsRunning) { await _apiServer.StopAsync(); ApiStatus = "Stopped"; Dashboard.UpdateApiStatus("Stopped", _apiPort); }
        }
        catch { }
    }

    public ApiServer GetApiServer() => _apiServer;

    private void RefreshStats()
    {
        Dashboard.TotalCount = Proxies.Count;
        Dashboard.RunningCount = Proxies.Count(p => p.Status == ProxyStatus.Running);
        Dashboard.StoppedCount = Proxies.Count(p => p.Status == ProxyStatus.Stopped);
        Dashboard.FailedCount = Proxies.Count(p => p.Status == ProxyStatus.Failed);
        TrafficStats.RefreshStats(Proxies);
    }

    private void PersistProxyState()
    {
        try
        {
            var cfg = _proxyDataService.Load();
            foreach (var p in cfg.Proxies)
            {
                var live = Proxies.FirstOrDefault(x => x.Id == p.Id);
                if (live is not null) p.Status = live.Status;
            }
            _proxyDataService.Save(cfg);
        }
        catch (Exception ex) { _logger.Warn($"PersistProxyState: failed to save config.json: {ex.Message}"); }
    }

    // ================================================================
    // Theme
    // ================================================================
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ThemeService.Instance.ToggleTheme();
        AddLog($"[{DateTime.Now:HH:mm:ss}] Theme switched to {(IsDarkTheme ? "Dark" : "Light")}");
    }

    // ================================================================
    // Group Management
    // ================================================================
    private void ShowManageGroupsWindow()
    {
        var vm = new ViewModels.ManageGroupsViewModel(
            Groups.Groups,
            onAddGroup: name => Groups.AddGroup(name),
            onDeleteGroup: name => Groups.RemoveGroup(name),
            onRenameGroup: (oldName, newName) => Groups.RenameGroup(oldName, newName));

        var win = new Views.ManageGroupsWindow
        {
            Owner = Application.Current?.MainWindow,
            DataContext = vm,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        win.ShowDialog();
        Groups.LoadGroupsFromProxies(ProxyList.Proxies);
    }

    private void StartGroupProxies()
    {
        if (Groups.SelectedGroup == "全部" || string.IsNullOrEmpty(Groups.SelectedGroup)) return;
        var groupProxies = ProxyList.Proxies.Where(p => p.Group == Groups.SelectedGroup && p.Status != ProxyStatus.Running).ToList();
        ProxyOperations.BatchStart(groupProxies);
        RefreshStats();
        AddLog($"[{DateTime.Now:HH:mm:ss}] Started group: {Groups.SelectedGroup}");
    }

    private void StopGroupProxies()
    {
        if (Groups.SelectedGroup == "全部" || string.IsNullOrEmpty(Groups.SelectedGroup)) return;
        var groupProxies = ProxyList.Proxies.Where(p => p.Group == Groups.SelectedGroup && p.Status == ProxyStatus.Running).ToList();
        ProxyOperations.BatchStop(groupProxies);
        RefreshStats();
        AddLog($"[{DateTime.Now:HH:mm:ss}] Stopped group: {Groups.SelectedGroup}");
    }
}
