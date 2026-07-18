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
using YLproxy.Core;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;
using GlobalConfigService = YLproxy.Infrastructure.AppSettingsService;
using GlobalProxyConfig = YLproxy.Infrastructure.ProxyConfig;
using GlobalThreeProxyConfig = YLproxy.Infrastructure.ThreeProxyConfig;
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

    // --- Host Info ---
    private string _computerName = string.Empty;
    public string ComputerName { get => _computerName; set => SetProperty(ref _computerName, value); }

    private string _ipAddress = "";
    public string IpAddress { get => _ipAddress; set => SetProperty(ref _ipAddress, value); }

    private string _networkStatus = "Unknown";
    public string NetworkStatus { get => _networkStatus; set => SetProperty(ref _networkStatus, value); }

    private DateTime _now = DateTime.Now;
    public DateTime Now { get => _now; set => SetProperty(ref _now, value); }

    // --- Proxy Collections ---
    public ObservableCollection<ProxyItem> Proxies { get; } = new();

    private readonly ObservableCollection<ProxyItem> _filteredProxies = new();
    public ObservableCollection<ProxyItem> FilteredProxies => _filteredProxies;

    public List<ProxyItem> SelectedProxies { get; set; } = new();

    // --- Log Collections ---
    private readonly ObservableCollection<LogEntry> _logs = new();
    private readonly ObservableCollection<LogEntry> _filteredLogs = new();
    public ObservableCollection<LogEntry> FilteredLogs => _filteredLogs;

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
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value, nameof(SearchText));
            ApplyProxyFilter();
        }
    }

    // --- Log Level Filter ---
    public List<string> LogLevels { get; } = new() { "全部", "Info", "Warn", "Error" };

    private string _selectedLogLevel = "全部";
    public string SelectedLogLevel
    {
        get => _selectedLogLevel;
        set
        {
            SetProperty(ref _selectedLogLevel, value, nameof(SelectedLogLevel));
            ApplyLogFilter();
        }
    }

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

    // --- Stats ---
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

    // ================================================================
    public MainViewModel(
        ILogger logger,
        GlobalConfigService settingsService,
        GlobalProxyConfig proxyConfig,
        GlobalThreeProxyConfig threeProxyConfig)
    {
        _logger = logger;
        _settingsService = settingsService;
        _proxyConfig = proxyConfig;
        _threeProxyConfig = threeProxyConfig;
        YLproxy.Proxy.ProxyProcessManager.Configure(_threeProxyConfig);

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
        ClearLogCommand = new RelayCommand(() => { _logs.Clear(); _filteredLogs.Clear(); });
        BatchStartCommand = new RelayCommand(BatchStart, () => SelectedProxies.Count > 0);
        BatchStopCommand = new RelayCommand(BatchStop, () => SelectedProxies.Count > 0);
        ExportCommand = new RelayCommand(ExportToJson);
        ImportCommand = new RelayCommand(ImportFromJson);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

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
        _filteredProxies.Clear();
        var query = string.IsNullOrWhiteSpace(_searchText)
            ? Proxies
            : Proxies.Where(p =>
                (p.Name?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.RemoteHost?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.RemotePort.ToString().Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                (p.Username?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Group?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.LocalPort.ToString().Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        foreach (var p in query)
            _filteredProxies.Add(p);
    }

    private void ApplyLogFilter()
    {
        _filteredLogs.Clear();
        IEnumerable<LogEntry> source = _selectedLogLevel switch
        {
            "Info" => _logs.Where(l => l.Level != LogLevel.Debug),
            "Warn" => _logs.Where(l => l.Level is LogLevel.Warn or LogLevel.Error or LogLevel.Fatal),
            "Error" => _logs.Where(l => l.Level is LogLevel.Error or LogLevel.Fatal),
            _ => _logs,
        };

        foreach (var l in source)
            _filteredLogs.Add(l);
    }

    // ================================================================
    // Ticking
    // ================================================================
    private void RefreshDataGrid() { }

    private void Tick()
    {
        var now = DateTime.Now;
        var netStatus = GetNetworkStatus();
        var ip = YLproxy.Utils.NetworkUtil.GetBestLocalIp();

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            Now = now;
            NetworkStatus = netStatus;
            if (!string.IsNullOrWhiteSpace(ip)) IpAddress = ip;
        });
    }

    // ================================================================
    // Init & Config
    // ================================================================
    private void InitFromConfig()
    {
        Proxies.Clear();
        _filteredProxies.Clear();
        try
        {
            var configPath = GetConfigPath();
            var svc = new YLproxy.Core.Config.ProxyDataService(configPath);
            var cfg = svc.Load();

            foreach (var p in cfg.Proxies)
                Proxies.Add(p);

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
            var vm = new ViewModels.AddProxyViewModel(
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
            var vm = new ViewModels.AddProxyViewModel(
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

            try { YLproxy.Proxy.ProxyProcessManager.Stop(proxy); }
            catch { }

            Proxies.Remove(proxy);
            ApplyProxyFilter();

            var configPath = GetConfigPath();
            var svc = new YLproxy.Core.Config.ProxyDataService(configPath);
            var cfg = svc.Load();
            cfg.Proxies.RemoveAll(p => p.Id == proxy.Id);
            svc.Save(cfg);

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
        if (IsTesting) return;
        IsTesting = true;

        try
        {
            if (SelectedProxy is null)
            {
                SetStatus("Test failed: no proxy selected");
                IsTesting = false;
                return;
            }

            var p = SelectedProxy;
            SetStatus($"Testing {p.Name}...");

            var (success, latency, error) = await ProxyTester.TestAsync(
                p.RemoteHost, p.RemotePort, p.Username, p.Password);

            if (success)
            {
                AddLog($"[{DateTime.Now:HH:mm:ss}] Test OK — {p.Name}: {latency}ms");
                SetStatus($"{p.Name}: test passed ({latency}ms)");
            }
            else
            {
                AddLog($"[{DateTime.Now:HH:mm:ss}] Test FAILED — {p.Name}: {error}");
                SetStatus($"{p.Name}: test failed — {error}");
            }
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Test exception: {ex.Message}");
            SetStatus($"Test error: {ex.Message}");
        }
        finally
        {
            IsTesting = false;
        }
    }

    // ================================================================
    // Start / Stop (single)
    // ================================================================
    private void StartSelectedProxy()
    {
        if (IsStarting) return;
        IsStarting = true;

        try
        {
            if (SelectedProxy is null)
            {
                SetStatus("Start failed: no proxy selected");
                IsStarting = false;
                return;
            }

            var p = SelectedProxy;
            _ = Task.Run(() =>
            {
                try
                {
                    p.Status = ProxyStatus.Running;
                    YLproxy.Proxy.ProxyProcessManager.Start(p);
                    Application.Current?.Dispatcher.BeginInvoke(() => RefreshStats());
                    AddLog($"[{DateTime.Now:HH:mm:ss}] Started: {p.LocalHost}:{p.LocalPort} -> {p.RemoteHost}:{p.RemotePort}");
                    SetStatus($"{p.Name}: started on port {p.LocalPort}");
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
        if (IsStopping) return;
        IsStopping = true;

        try
        {
            if (SelectedProxy is null)
            {
                SetStatus("Stop failed: no proxy selected");
                IsStopping = false;
                return;
            }

            var p = SelectedProxy;
            _ = Task.Run(() =>
            {
                try
                {
                    YLproxy.Proxy.ProxyProcessManager.Stop(p);
                    p.Status = ProxyStatus.Stopped;
                    Application.Current?.Dispatcher.BeginInvoke(() => RefreshStats());
                    AddLog($"[{DateTime.Now:HH:mm:ss}] Stopped: {p.LocalHost}:{p.LocalPort}");
                    SetStatus($"{p.Name}: stopped");
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

    // ================================================================
    // Batch Operations
    // ================================================================
    private void BatchStart()
    {
        if (IsStarting) return;
        IsStarting = true;

        var targets = SelectedProxies
            .Where(p => p.Status != ProxyStatus.Running)
            .ToList();

        if (targets.Count == 0)
        {
            SetStatus("No stopped proxies selected for batch start.");
            IsStarting = false;
            return;
        }

        _ = Task.Run(() =>
        {
            var started = 0;
            foreach (var p in targets)
            {
                try
                {
                    p.Status = ProxyStatus.Running;
                    YLproxy.Proxy.ProxyProcessManager.Start(p);
                    started++;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] Batch start: {p.Name} ({p.LocalPort})");
                }
                catch (Exception ex)
                {
                    p.Status = ProxyStatus.Failed;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] Batch start failed — {p.Name}: {ex.Message}");
                }
            }
            Application.Current?.Dispatcher.BeginInvoke(() => RefreshStats());
            SetStatus($"Batch started: {started}/{targets.Count} proxies");
            IsStarting = false;
        });
    }

    private void BatchStop()
    {
        if (IsStopping) return;
        IsStopping = true;

        var targets = SelectedProxies
            .Where(p => p.Status == ProxyStatus.Running)
            .ToList();

        if (targets.Count == 0)
        {
            SetStatus("No running proxies selected for batch stop.");
            IsStopping = false;
            return;
        }

        _ = Task.Run(() =>
        {
            var stopped = 0;
            foreach (var p in targets)
            {
                try
                {
                    YLproxy.Proxy.ProxyProcessManager.Stop(p);
                    p.Status = ProxyStatus.Stopped;
                    stopped++;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] Batch stop: {p.Name} ({p.LocalPort})");
                }
                catch (Exception ex)
                {
                    p.Status = ProxyStatus.Failed;
                    AddLog($"[{DateTime.Now:HH:mm:ss}] Batch stop failed — {p.Name}: {ex.Message}");
                }
            }
            Application.Current?.Dispatcher.BeginInvoke(() => RefreshStats());
            SetStatus($"Batch stopped: {stopped}/{targets.Count} proxies");
            IsStopping = false;
        });
    }

    // ================================================================
    // Import / Export
    // ================================================================
    private void ExportToJson()
    {
        if (IsExporting) return;
        IsExporting = true;

        try
        {
            // Use SaveFileDialog via Microsoft.Win32
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = $"ylproxy_export_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() != true)
            {
                IsExporting = false;
                return;
            }

            var exportProxies = SelectedProxies.Count > 0
                ? SelectedProxies
                : Proxies.ToList();

            var exportData = new
            {
                ExportedAt = DateTime.UtcNow.ToString("O"),
                Count = exportProxies.Count,
                Proxies = exportProxies.Select(p => new
                {
                    p.Name, p.RemoteHost, p.RemotePort,
                    Username = string.IsNullOrWhiteSpace(p.Username) ? "" : "(exported)",
                    Password = string.IsNullOrWhiteSpace(p.Password) ? "" : "(exported)",
                    p.Group, p.LocalHost, p.LocalPort
                })
            };

            var json = JsonSerializer.Serialize(exportData,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(dialog.FileName, json, System.Text.Encoding.UTF8);

            SetStatus($"Exported {exportProxies.Count} proxies to {Path.GetFileName(dialog.FileName)}");
            AddLog($"[{DateTime.Now:HH:mm:ss}] Export: {exportProxies.Count} proxies → {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Export failed: {ex.Message}");
        }
        finally
        {
            IsExporting = false;
        }
    }

    private void ImportFromJson()
    {
        if (IsImporting) return;
        IsImporting = true;

        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                IsImporting = false;
                return;
            }

            var json = File.ReadAllText(dialog.FileName, System.Text.Encoding.UTF8);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("Proxies", out var proxiesEl)
                || proxiesEl.ValueKind != JsonValueKind.Array)
            {
                SetStatus("Invalid export file: missing 'Proxies' array.");
                IsImporting = false;
                return;
            }

            var configPath = GetConfigPath();
            var svc = new YLproxy.Core.Config.ProxyDataService(configPath);
            var cfg = svc.Load();

            var maxId = cfg.Proxies.Count > 0 ? cfg.Proxies.Max(p => p.Id) : 0;
            var usedPorts = new HashSet<int>(cfg.Proxies.Select(p => p.LocalPort));

            var imported = 0;
            foreach (var proxyEl in proxiesEl.EnumerateArray())
            {
                try
                {
                    var name = proxyEl.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var host = proxyEl.TryGetProperty("RemoteHost", out var rh) ? rh.GetString() ?? "" : "";
                    var port = proxyEl.TryGetProperty("RemotePort", out var rp) ? rp.GetInt32() : 0;
                    var group = proxyEl.TryGetProperty("Group", out var gr) ? gr.GetString() ?? "" : "";

                    if (string.IsNullOrWhiteSpace(host) || port <= 0) continue;

                    var localPort = _proxyConfig.PortRangeStart;
                    while (usedPorts.Contains(localPort))
                    {
                        localPort++;
                        if (localPort > _proxyConfig.PortRangeEnd) break;
                    }

                    if (localPort > _proxyConfig.PortRangeEnd) break;

                    usedPorts.Add(localPort);
                    maxId++;

                    cfg.Proxies.Add(new ProxyItem
                    {
                        Id = maxId,
                        Name = name,
                        RemoteHost = host,
                        RemotePort = port,
                        Username = "",
                        Password = "",
                        Group = group,
                        LocalHost = "127.0.0.1",
                        LocalPort = localPort,
                        Status = ProxyStatus.Stopped,
                        CreateTime = DateTime.UtcNow
                    });

                    imported++;
                }
                catch { }
            }

            svc.Save(cfg);
            InitFromConfig();
            RefreshStats();

            SetStatus($"Imported {imported} proxies from {Path.GetFileName(dialog.FileName)}");
            AddLog($"[{DateTime.Now:HH:mm:ss}] Import: {imported} proxies from {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Import failed: {ex.Message}");
            SetStatus($"Import failed: {ex.Message}");
        }
        finally
        {
            IsImporting = false;
        }
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
                YLproxy.Proxy.ProxyProcessManager.Stop(proxy);
                Thread.Sleep(500);
                YLproxy.Proxy.ProxyProcessManager.Start(proxy);
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
            _logs.Add(entry);
            _filteredLogs.Add(entry);
        });

        try { _logger.Info(message); }
        catch { }
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
        ComputerName = Environment.MachineName;
        NetworkStatus = GetNetworkStatus();
        IpAddress = YLproxy.Utils.NetworkUtil.GetBestLocalIp() ?? "";
        Now = DateTime.Now;
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

    public async Task ShutdownAsync()
    {
        foreach (var proxy in Proxies.Where(p => p.Status == ProxyStatus.Running).ToList())
        {
            try { YLproxy.Proxy.ProxyProcessManager.Stop(proxy); } catch { }
        }
        await Task.CompletedTask;
    }

    private void RefreshStats()
    {
        TotalCount = Proxies.Count;
        RunningCount = Proxies.Count(p => p.Status == ProxyStatus.Running);
        StoppedCount = Proxies.Count(p => p.Status == ProxyStatus.Stopped);
        FailedCount = Proxies.Count(p => p.Status == ProxyStatus.Failed);
    }

    private void PersistProxyState()
    {
        try
        {
            var configPath = GetConfigPath();
            var svc = new YLproxy.Core.Config.ProxyDataService(configPath);
            var cfg = svc.Load();
            var proxyList = Proxies.ToList();
            foreach (var p in cfg.Proxies)
            {
                var live = proxyList.FirstOrDefault(x => x.Id == p.Id);
                if (live is not null)
                    p.Status = live.Status;
            }
            svc.Save(cfg);
        }
        catch (Exception ex)
        {
            _logger.Warn($"PersistProxyState: failed to save config.json: {ex.Message}");
        }
    }
}
