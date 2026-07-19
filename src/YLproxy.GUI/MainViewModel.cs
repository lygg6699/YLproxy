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
using YLproxy.GUI.ViewModels;
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

    private readonly Core.Abstractions.IProxyDataService _proxyDataService;
    private readonly Core.Abstractions.IProxyTester _proxyTester;
    private readonly Proxy.Abstractions.IProxyProcessManager _proxyProcessManager;

    // --- Sub-ViewModels ---
    public HostInfoViewModel HostInfo { get; } = new();
    public DashboardViewModel Dashboard { get; } = new();
    public LogPanelViewModel LogPanel { get; } = new();

    // --- Proxy Collections ---
    public ObservableCollection<ProxyItem> Proxies { get; } = new();

    private readonly ObservableCollection<ProxyItem> _filteredProxies = new();
    public ObservableCollection<ProxyItem> FilteredProxies => _filteredProxies;

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
        Proxy.Abstractions.IProxyProcessManager proxyProcessManager)
    {
        _logger = logger;
        _settingsService = settingsService;
        _proxyConfig = proxyConfig;
        _threeProxyConfig = threeProxyConfig;
        _proxyDataService = proxyDataService;
        _proxyTester = proxyTester;
        _proxyProcessManager = proxyProcessManager;
        _proxyProcessManager.Configure(_threeProxyConfig);

        InitFromConfig();
        LoadHostInfo();
        RefreshStats();
        AddLog($"[{DateTime.Now:HH:mm:ss}] Application started. (YLproxy v0.2.0)");

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
        Proxies.Clear();
        _filteredProxies.Clear();
        try
        {
            var cfg = _proxyDataService.Load();

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
            catch { }

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

            var (success, latency, error) = await _proxyTester.TestAsync(
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
                    _proxyProcessManager.Start(p);
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
                    _proxyProcessManager.Stop(p);
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
                    _proxyProcessManager.Start(p);
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
                    _proxyProcessManager.Stop(p);
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

            var cfg = _proxyDataService.Load();

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
                // Skip invalid proxy entries and continue with the next one
                catch { }
            }

            _proxyDataService.Save(cfg);
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
        catch { }
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
        catch
        {
            return "Unknown";
        }
    }

    public async Task ShutdownAsync()
    {
        // Attempt to stop each proxy, but continue shutdown even if stopping fails
        foreach (var proxy in Proxies.Where(p => p.Status == ProxyStatus.Running).ToList())
        {
            try { _proxyProcessManager.Stop(proxy); } catch { }
        }
        await Task.CompletedTask;
    }

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
