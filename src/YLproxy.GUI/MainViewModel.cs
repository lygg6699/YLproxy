using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using YLproxy.Api;
using YLproxy.Core;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;
using Application = System.Windows.Application;
using Timer = System.Threading.Timer;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using GlobalConfigService = YLproxy.Infrastructure.AppSettingsService;
using GlobalProxyConfig = YLproxy.Infrastructure.ProxyConfig;
using GlobalThreeProxyConfig = YLproxy.Infrastructure.ThreeProxyConfig;
using GlobalApiConfig = YLproxy.Infrastructure.ApiConfig;

namespace YLproxy.GUI;

public sealed class LogItem
{
    public string Text { get; }
    public LogLevel Level { get; }
    public LogItem(string text)
    {
        Text = text;
        var upper = text.ToUpperInvariant();
        if (upper.Contains("[ERROR]") || upper.Contains("FATAL]") || upper.Contains("失败"))
            Level = LogLevel.Error;
        else if (upper.Contains("[WARN]"))
            Level = LogLevel.Warn;
        else
            Level = LogLevel.Info;
    }
}

public enum LogLevel { Info, Warn, Error }

public sealed class MainViewModel : ViewModelBase
{
    private readonly Timer _timer;
    private readonly MonitorService _monitorService;
    private readonly GlobalConfigService _settingsService;
    private readonly ILogger _logger;
    private readonly GlobalProxyConfig _proxyConfig;
    private readonly GlobalThreeProxyConfig _threeProxyConfig;
    private readonly GlobalApiConfig _apiConfig;
    private readonly ApiServer _apiServer;
    private readonly ObservableCollection<LogItem> _allLogs = new();

    private string _computerName = string.Empty;
    private string _ipAddress = "";
    private string _networkStatus = "Unknown";
    private DateTime _now = DateTime.Now;

    public string ComputerName { get => _computerName; set => SetProperty(ref _computerName, value); }
    public string IpAddress { get => _ipAddress; set => SetProperty(ref _ipAddress, value); }
    public string NetworkStatus { get => _networkStatus; set => SetProperty(ref _networkStatus, value); }
    public DateTime Now { get => _now; set => SetProperty(ref _now, value); }

    public ObservableCollection<ProxyItem> Proxies { get; } = new();
    public ObservableCollection<LogItem> FilteredLogs { get; } = new();

    public RelayCommand AddCommand { get; }
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

    private bool _isTesting;
    public bool IsTesting { get => _isTesting; set => SetProperty(ref _isTesting, value); }

    private bool _isStarting;
    public bool IsStarting { get => _isStarting; set => SetProperty(ref _isStarting, value); }

    private bool _isStopping;
    public bool IsStopping { get => _isStopping; set => SetProperty(ref _isStopping, value); }

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

    private List<ProxyItem> _selectedProxies = new();
    public List<ProxyItem> SelectedProxies
    {
        get => _selectedProxies;
        set => SetProperty(ref _selectedProxies, value);
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value ?? string.Empty);
            ApplySearchFilter();
        }
    }

    private LogLevel _selectedLogLevel = LogLevel.Info;
    public LogLevel SelectedLogLevel
    {
        get => _selectedLogLevel;
        set
        {
            SetProperty(ref _selectedLogLevel, value);
            ApplyLogFilter();
        }
    }

    public ObservableCollection<LogLevel> LogLevels { get; } = new() { LogLevel.Info, LogLevel.Warn, LogLevel.Error };

    private readonly ObservableCollection<ProxyItem> _filteredProxies = new();
    public ObservableCollection<ProxyItem> FilteredProxies => _filteredProxies;

    public MainViewModel()
    {
        _settingsService = new GlobalConfigService();
        _logger = LoggerFactory.CreateLogger();
        _proxyConfig = _settingsService.GetSection<GlobalProxyConfig>("Proxy");
        _threeProxyConfig = _settingsService.GetSection<GlobalThreeProxyConfig>("ThreeProxy");
        _apiConfig = _settingsService.GetSection<GlobalApiConfig>("Api");
        YLproxy.Proxy.ProxyProcessManager.Configure(_threeProxyConfig);

        _apiServer = new ApiServer(
            GetConfigPath(),
            _proxyConfig,
            _apiConfig.Port,
            _apiConfig.AccessToken);

        InitFromConfig();
        LoadHostInfo();
        RefreshStats();
        AddLog($"[{DateTime.Now:HH:mm:ss}] Application started (Phase 9: REST API).");

        _ = StartApiServer();

        AddCommand = new RelayCommand(ShowAddWindow);
        RemoveCommand = new RelayCommand(RemoveSelectedProxyAndPersist);
        TestCommand = new RelayCommand(() => _ = TestSelectedProxyAsync());
        StartCommand = new RelayCommand(StartSelectedProxy);
        StopCommand = new RelayCommand(StopSelectedProxy);
        ClearLogCommand = new RelayCommand(() => { _allLogs.Clear(); ApplyLogFilter(); });
        BatchStartCommand = new RelayCommand(BatchStart);
        BatchStopCommand = new RelayCommand(BatchStop);
        ExportCommand = new RelayCommand(ExportProxies);
        ImportCommand = new RelayCommand(ImportProxies);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        Proxies.CollectionChanged += (_, _) => { ApplySearchFilter(); RefreshStats(); };

        _monitorService = new MonitorService(
            getProxies: () => Proxies.ToList(),
            logAction: (msg) => AddLog(msg),
            refreshAction: RefreshDataGrid,
            checkInterval: TimeSpan.FromSeconds(Math.Max(1, _proxyConfig.CheckIntervalSeconds)));

        _timer = new Timer(_ => Tick(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void RefreshDataGrid() { }

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
                Proxies.Add(p);
            if (Proxies.Count == 0)
                AddLog($"[{DateTime.Now:HH:mm:ss}] config.json loaded: 0 proxies.");
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] config.json load failed: {ex.Message}");
        }
        ApplySearchFilter();
    }

    private string GetConfigPath() =>
        PathResolver.ResolvePath(_proxyConfig.DataDirectory, _proxyConfig.ConfigFileName);

    private async Task TestSelectedProxyAsync()
    {
        if (IsTesting) return;
        IsTesting = true;
        try
        {
            if (SelectedProxy is null) { AddLog($"[{DateTime.Now:HH:mm:ss}] [WARN] Test: no proxy selected"); return; }
            var p = SelectedProxy;
            AddLog($"[{DateTime.Now:HH:mm:ss}] Testing: {p.RemoteHost}:{p.RemotePort} ...");
            var result = await YLproxy.Core.ProxyTester.TestAsync(
                p.RemoteHost, p.RemotePort,
                string.IsNullOrWhiteSpace(p.Username) ? null : p.Username,
                string.IsNullOrWhiteSpace(p.Password) ? null : p.Password);
            if (result.Success)
                AddLog($"[{DateTime.Now:HH:mm:ss}] 成功：延迟 {result.LatencyMs}ms");
            else
                AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] 失败：{result.Error ?? "连接失败"}");
        }
        catch (Exception ex) { AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] Test: {ex.Message}"); }
        finally { IsTesting = false; }
    }

    private void StartSelectedProxy()
    {
        if (IsStarting) return;
        IsStarting = true;
        try
        {
            if (SelectedProxy is null) { AddLog($"[{DateTime.Now:HH:mm:ss}] [WARN] Start: no proxy selected"); IsStarting = false; return; }
            var p = SelectedProxy;
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    p.Status = ProxyStatus.Running;
                    YLproxy.Proxy.ProxyProcessManager.Start(p);
                    AddLog($"[{DateTime.Now:HH:mm:ss}] Started: {p.LocalHost}:{p.LocalPort}");
                    Application.Current?.Dispatcher.BeginInvoke(RefreshStats);
                }
                catch (Exception ex)
                {
                    p.Status = ProxyStatus.Failed;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] Start: {ex.Message}");
                    Application.Current?.Dispatcher.BeginInvoke(RefreshStats);
                }
                finally { IsStarting = false; }
            });
        }
        catch (Exception ex) { AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] Start: {ex.Message}"); IsStarting = false; }
    }

    private void StopSelectedProxy()
    {
        if (IsStopping) return;
        IsStopping = true;
        try
        {
            if (SelectedProxy is null) { AddLog($"[{DateTime.Now:HH:mm:ss}] [WARN] Stop: no proxy selected"); IsStopping = false; return; }
            var p = SelectedProxy;
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    YLproxy.Proxy.ProxyProcessManager.Stop(p);
                    p.Status = ProxyStatus.Stopped;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] Stopped: {p.LocalHost}:{p.LocalPort}");
                    Application.Current?.Dispatcher.BeginInvoke(RefreshStats);
                }
                catch (Exception ex)
                {
                    p.Status = ProxyStatus.Failed;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] Stop: {ex.Message}");
                    Application.Current?.Dispatcher.BeginInvoke(RefreshStats);
                }
                finally { IsStopping = false; }
            });
        }
        catch (Exception ex) { AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] Stop: {ex.Message}"); IsStopping = false; }
    }

    private void BatchStart()
    {
        if (IsStarting) return;
        var targets = _selectedProxies.Count > 0
            ? _selectedProxies.ToList()
            : (SelectedProxy is not null ? new List<ProxyItem> { SelectedProxy } : null);
        if (targets is null || targets.Count == 0) { AddLog($"[{DateTime.Now:HH:mm:ss}] [WARN] Batch start: no selection"); return; }

        IsStarting = true;
        AddLog($"[{DateTime.Now:HH:mm:ss}] Batch starting {targets.Count} proxy(s)...");
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            foreach (var p in targets)
            {
                try { p.Status = ProxyStatus.Running; YLproxy.Proxy.ProxyProcessManager.Start(p); AddLog($"[{DateTime.Now:HH:mm:ss}] Started: {p.LocalHost}:{p.LocalPort}"); }
                catch (Exception ex) { p.Status = ProxyStatus.Failed; AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] Start ({p.Name}): {ex.Message}"); }
            }
            IsStarting = false;
            Application.Current?.Dispatcher.BeginInvoke(RefreshStats);
        });
    }

    private void BatchStop()
    {
        if (IsStopping) return;
        var targets = _selectedProxies.Count > 0
            ? _selectedProxies.ToList()
            : (SelectedProxy is not null ? new List<ProxyItem> { SelectedProxy } : null);
        if (targets is null || targets.Count == 0) { AddLog($"[{DateTime.Now:HH:mm:ss}] [WARN] Batch stop: no selection"); return; }

        IsStopping = true;
        AddLog($"[{DateTime.Now:HH:mm:ss}] Batch stopping {targets.Count} proxy(s)...");
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            foreach (var p in targets)
            {
                try { YLproxy.Proxy.ProxyProcessManager.Stop(p); p.Status = ProxyStatus.Stopped; AddLog($"[{DateTime.Now:HH:mm:ss}] Stopped: {p.LocalHost}:{p.LocalPort}"); }
                catch (Exception ex) { p.Status = ProxyStatus.Failed; AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] Stop ({p.Name}): {ex.Message}"); }
            }
            IsStopping = false;
            Application.Current?.Dispatcher.BeginInvoke(RefreshStats);
        });
    }

    private void ExportProxies()
    {
        try
        {
            var dlg = new SaveFileDialog { Title = "导出代理列表", Filter = "JSON (*.json)|*.json", DefaultExt = ".json", FileName = $"ylproxy_export_{DateTime.Now:yyyyMMdd_HHmmss}.json" };
            if (dlg.ShowDialog() != true) return;
            var svc = new YLproxy.Core.Config.ProxyDataService(GetConfigPath());
            var cfg = svc.Load();
            var json = System.Text.Json.JsonSerializer.Serialize(cfg, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json);
            AddLog($"[{DateTime.Now:HH:mm:ss}] Exported {cfg.Proxies.Count} proxies → {dlg.FileName}");
        }
        catch (Exception ex) { AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] Export: {ex.Message}"); }
    }

    private void ImportProxies()
    {
        try
        {
            var dlg = new OpenFileDialog { Title = "导入代理列表", Filter = "JSON (*.json)|*.json" };
            if (dlg.ShowDialog() != true) return;
            var json = File.ReadAllText(dlg.FileName);
            var importCfg = System.Text.Json.JsonSerializer.Deserialize<YLproxy.Models.AppConfig>(json);
            if (importCfg?.Proxies is null || importCfg.Proxies.Count == 0) { AddLog($"[{DateTime.Now:HH:mm:ss}] [WARN] Import: empty file"); return; }

            var svc = new YLproxy.Core.Config.ProxyDataService(GetConfigPath());
            var currentCfg = svc.Load();
            var existingIds = new HashSet<int>(currentCfg.Proxies.Select(p => p.Id));
            var existingPorts = new HashSet<int>(currentCfg.Proxies.Select(p => p.LocalPort));
            int added = 0, skipped = 0;
            foreach (var p in importCfg.Proxies)
            {
                if (existingIds.Contains(p.Id) || existingPorts.Contains(p.LocalPort)) { skipped++; continue; }
                currentCfg.Proxies.Add(p);
                existingIds.Add(p.Id); existingPorts.Add(p.LocalPort); added++;
            }
            svc.Save(currentCfg);
            InitFromConfig();
            AddLog($"[{DateTime.Now:HH:mm:ss}] Import: {added} added, {skipped} skipped (duplicates)");
        }
        catch (Exception ex) { AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] Import: {ex.Message}"); }
    }

    private void ShowAddWindow()
    {
        try
        {
            var configPath = GetConfigPath();
            var vm = new YLproxy.GUI.ViewModels.AddProxyViewModel(
                Proxies.ToList(), configPath, _proxyConfig.PortRangeStart, _proxyConfig.PortRangeEnd);
            var win = new YLproxy.GUI.Views.AddProxyWindow { Owner = Application.Current?.MainWindow, DataContext = vm };
            vm.CloseAction = () => win.Dispatcher.BeginInvoke(new Action(() => win.DialogResult = true));
            win.ShowDialog();
            InitFromConfig();
        }
        catch (Exception ex) { AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] Add window: {ex.Message}"); }
    }

    private void RemoveSelectedProxyAndPersist()
    {
        try
        {
            if (SelectedProxy is null) { AddLog($"[{DateTime.Now:HH:mm:ss}] [WARN] Remove: no proxy selected"); return; }
            var id = SelectedProxy.Id;
            try { YLproxy.Proxy.ProxyProcessManager.Stop(SelectedProxy); } catch { }
            Proxies.Remove(SelectedProxy);

            var configPath = GetConfigPath();
            var svc = new YLproxy.Core.Config.ProxyDataService(configPath);
            var cfg = svc.Load();
            var before = cfg.Proxies.Count;
            cfg.Proxies.RemoveAll(p => p.Id == id);
            svc.Save(cfg);
            AddLog($"[{DateTime.Now:HH:mm:ss}] Removed proxy Id={id} (remaining: {cfg.Proxies.Count})");
        }
        catch (Exception ex) { AddLog($"[{DateTime.Now:HH:mm:ss}] [ERROR] Remove: {ex.Message}"); }
    }

    private void AddLog(string message)
    {
        var item = new LogItem(message);
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _allLogs.Add(item);
            ApplyLogFilter();
        });
        try { _logger.Info(message); } catch { }
    }

    private void ApplySearchFilter()
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _filteredProxies.Clear();
            var query = (_searchText ?? string.Empty).Trim().ToUpperInvariant();
            foreach (var p in Proxies)
            {
                if (string.IsNullOrEmpty(query)
                    || (p.Name ?? string.Empty).ToUpperInvariant().Contains(query)
                    || (p.RemoteHost ?? string.Empty).ToUpperInvariant().Contains(query)
                    || (p.Username ?? string.Empty).ToUpperInvariant().Contains(query))
                {
                    _filteredProxies.Add(p);
                }
            }
        });
    }

    private void ApplyLogFilter()
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            FilteredLogs.Clear();
            foreach (var log in _allLogs)
            {
                if (_selectedLogLevel == LogLevel.Info || log.Level >= _selectedLogLevel)
                    FilteredLogs.Add(log);
            }
        });
    }

    private void LoadHostInfo()
    {
        ComputerName = Environment.MachineName;
        NetworkStatus = GetNetworkStatus();
        IpAddress = GetBestLocalIp() ?? "";
        Now = DateTime.Now;
    }

    private static string GetNetworkStatus()
    {
        try { return NetworkInterface.GetIsNetworkAvailable() ? "Connected" : "Disconnected"; }
        catch { return "Unknown"; }
    }

    private static string? GetBestLocalIp()
    {
        try
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    return ip.ToString();
        }
        catch { }
        return null;
    }

    private void RefreshStats()
    {
        TotalCount = Proxies.Count;
        RunningCount = Proxies.Count(p => p.Status == ProxyStatus.Running);
        StoppedCount = Proxies.Count(p => p.Status == ProxyStatus.Stopped);
        FailedCount = Proxies.Count(p => p.Status == ProxyStatus.Failed);
    }

    private async Task StartApiServer()
    {
        try
        {
            await _apiServer.StartAsync();
            AddLog($"[{DateTime.Now:HH:mm:ss}] REST API started on http://127.0.0.1:{_apiServer.Port}");
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] [WARN] REST API failed to start: {ex.Message}");
        }
    }

    public async Task ShutdownAsync()
    {
        _timer?.Dispose();
        try { await _apiServer.StopAsync(); } catch { }
    }
}