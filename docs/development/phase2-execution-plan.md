# 阶段二：代码质量提升执行方案

> **执行环境：** Windows本地 + 云空间CI验证
> **预计工时：** 60小时
> **进度目标：** 75% → 85%
> **执行人：** 本地AI

---

## 执行前准备

### 环境检查
```powershell
# 确认当前分支
git branch

# 确认工作区干净
git status

# 拉取最新代码
git pull origin main

# 创建功能分支
git checkout -b phase2-code-quality-improvement
```

### 备份当前状态
```powershell
# 创建备份标签
git tag backup-before-phase2

# 备份关键文件
Copy-Item src\YLproxy.GUI\MainViewModel.cs src\YLproxy.GUI\MainViewModel.cs.backup
```

---

## 子阶段 2.1：MainViewModel 拆分（75% → 78%）

### 目标
- 分析 MainViewModel 的 894 行代码职责
- 提取 ProxyListViewModel（代理列表管理）
- 提取 ProxyOperationViewModel（启动/停止/测试操作）
- 提取 ImportExportViewModel（导入导出功能）
- 保持现有功能不变，仅重构结构
- 更新单元测试适配新结构

### 执行步骤

#### 步骤 2.1.1：分析 MainViewModel 职责
```powershell
# 查看当前 MainViewModel 结构
Get-Content src\YLproxy.GUI\MainViewModel.cs | Measure-Object -Line

# 分析职责分布
# - 代理集合管理：Proxies, FilteredProxies, SelectedProxies
# - 代理操作：Add, Edit, Remove, Test, Start, Stop
# - 批量操作：BatchStart, BatchStop
# - 导入导出：Export, Import
# - 搜索过滤：SearchText, ApplyProxyFilter
# - 日志记录：AddLog, SetStatus
# - 主机信息：HostInfo
# - 仪表盘：Dashboard
# - 日志面板：LogPanel
```

#### 步骤 2.1.2：创建 ProxyListViewModel
```powershell
# 创建新文件
New-Item -Path "src/YLproxy.GUI/ViewModels/ProxyListViewModel.cs" -ItemType File
```

**文件内容：**
```csharp
using System.Collections.ObjectModel;
using System.Linq;
using YLproxy.Models;

namespace YLproxy.GUI.ViewModels;

/// <summary>
/// 负责代理列表的管理，包括代理集合、过滤、选择等功能
/// </summary>
public sealed class ProxyListViewModel : ViewModelBase
{
    private readonly ObservableCollection<ProxyItem> _proxies = new();
    private readonly ObservableCollection<ProxyItem> _filteredProxies = new();
    private string _searchText = string.Empty;

    public ObservableCollection<ProxyItem> Proxies => _proxies;
    public ObservableCollection<ProxyItem> FilteredProxies => _filteredProxies;

    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value, nameof(SearchText));
            ApplyProxyFilter();
        }
    }

    public ProxyListViewModel()
    {
        ApplyProxyFilter();
    }

    /// <summary>
    /// 添加代理到列表
    /// </summary>
    public void AddProxy(ProxyItem proxy)
    {
        _proxies.Add(proxy);
        ApplyProxyFilter();
    }

    /// <summary>
    /// 从列表中移除代理
    /// </summary>
    public void RemoveProxy(ProxyItem proxy)
    {
        _proxies.Remove(proxy);
        ApplyProxyFilter();
    }

    /// <summary>
    /// 清空代理列表
    /// </summary>
    public void ClearProxies()
    {
        _proxies.Clear();
        _filteredProxies.Clear();
    }

    /// <summary>
    /// 根据搜索文本过滤代理列表
    /// </summary>
    private void ApplyProxyFilter()
    {
        _filteredProxies.Clear();
        var query = string.IsNullOrWhiteSpace(_searchText)
            ? _proxies
            : _proxies.Where(p =>
                (p.Name?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.RemoteHost?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.RemotePort.ToString().Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                (p.Username?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Group?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.LocalPort.ToString().Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        foreach (var p in query)
            _filteredProxies.Add(p);
    }

    /// <summary>
    /// 刷新过滤列表（用于外部数据更新后）
    /// </summary>
    public void RefreshFilter()
    {
        var current = SearchText;
        SearchText = current;
    }
}
```

#### 步骤 2.1.3：创建 ProxyOperationViewModel
```powershell
# 创建新文件
New-Item -Path "src/YLproxy.GUI/ViewModels/ProxyOperationViewModel.cs" -ItemType File
```

**文件内容：**
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YLproxy.Core;
using YLproxy.Core.Abstractions;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Proxy.Abstractions;

namespace YLproxy.GUI.ViewModels;

/// <summary>
/// 负责代理操作，包括启动、停止、测试等
/// </summary>
public sealed class ProxyOperationViewModel : ViewModelBase
{
    private readonly IProxyTester _proxyTester;
    private readonly IProxyProcessManager _proxyProcessManager;
    private readonly ILogger _logger;
    private bool _isTesting;
    private bool _isStarting;
    private bool _isStopping;

    public bool IsTesting
    {
        get => _isTesting;
        set => SetProperty(ref _isTesting, value);
    }

    public bool IsStarting
    {
        get => _isStarting;
        set => SetProperty(ref _isStarting, value);
    }

    public bool IsStopping
    {
        get => _isStopping;
        set => SetProperty(ref _isStopping, value);
    }

    public ProxyOperationViewModel(
        IProxyTester proxyTester,
        IProxyProcessManager proxyProcessManager,
        ILogger logger)
    {
        _proxyTester = proxyTester;
        _proxyProcessManager = proxyProcessManager;
        _logger = logger;
    }

    /// <summary>
    /// 测试代理连通性
    /// </summary>
    public async Task<(bool Success, int? Latency, string? Error)> TestProxyAsync(ProxyItem proxy)
    {
        if (IsTesting) return (false, null, "Already testing");

        IsTesting = true;
        try
        {
            var (success, latency, error) = await _proxyTester.TestAsync(
                proxy.RemoteHost, proxy.RemotePort, proxy.Username, proxy.Password);

            return (success, latency, error);
        }
        catch (Exception ex)
        {
            _logger.Error($"Test proxy failed: {ex.Message}");
            return (false, null, ex.Message);
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    /// 启动代理
    /// </summary>
    public void StartProxy(ProxyItem proxy)
    {
        if (IsStarting) return;

        IsStarting = true;
        try
        {
            proxy.Status = ProxyStatus.Running;
            _proxyProcessManager.Start(proxy);
            _logger.Info($"Started proxy: {proxy.Name} ({proxy.LocalPort})");
        }
        catch (Exception ex)
        {
            proxy.Status = ProxyStatus.Failed;
            _logger.Error($"Start proxy failed: {ex.Message}");
            throw;
        }
        finally
        {
            IsStarting = false;
        }
    }

    /// <summary>
    /// 停止代理
    /// </summary>
    public void StopProxy(ProxyItem proxy)
    {
        if (IsStopping) return;

        IsStopping = true;
        try
        {
            _proxyProcessManager.Stop(proxy);
            proxy.Status = ProxyStatus.Stopped;
            _logger.Info($"Stopped proxy: {proxy.Name} ({proxy.LocalPort})");
        }
        catch (Exception ex)
        {
            proxy.Status = ProxyStatus.Failed;
            _logger.Error($"Stop proxy failed: {ex.Message}");
            throw;
        }
        finally
        {
            IsStopping = false;
        }
    }

    /// <summary>
    /// 批量启动代理
    /// </summary>
    public (int Started, int Failed) BatchStart(IEnumerable<ProxyItem> proxies)
    {
        var started = 0;
        var failed = 0;

        foreach (var proxy in proxies.Where(p => p.Status != ProxyStatus.Running))
        {
            try
            {
                StartProxy(proxy);
                started++;
            }
            catch
            {
                failed++;
            }
        }

        return (started, failed);
    }

    /// <summary>
    /// 批量停止代理
    /// </summary>
    public (int Stopped, int Failed) BatchStop(IEnumerable<ProxyItem> proxies)
    {
        var stopped = 0;
        var failed = 0;

        foreach (var proxy in proxies.Where(p => p.Status == ProxyStatus.Running))
        {
            try
            {
                StopProxy(proxy);
                stopped++;
            }
            catch
            {
                failed++;
            }
        }

        return (stopped, failed);
    }
}
```

#### 步骤 2.1.4：创建 ImportExportViewModel
```powershell
# 创建新文件
New-Item -Path "src/YLproxy.GUI/ViewModels/ImportExportViewModel.cs" -ItemType File
```

**文件内容：**
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;
using YLproxy.Infrastructure;
using YLproxy.Models;

namespace YLproxy.GUI.ViewModels;

/// <summary>
/// 负责代理配置的导入导出功能
/// </summary>
public sealed class ImportExportViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private bool _isExporting;
    private bool _isImporting;

    public bool IsExporting
    {
        get => _isExporting;
        set => SetProperty(ref _isExporting, value);
    }

    public bool IsImporting
    {
        get => _isImporting;
        set => SetProperty(ref _isImporting, value);
    }

    public ImportExportViewModel(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 导出代理配置到JSON文件
    /// </summary>
    public (bool Success, string? FilePath, int Count) ExportProxies(IEnumerable<ProxyItem> proxies)
    {
        if (IsExporting) return (false, null, 0);

        IsExporting = true;
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = $"ylproxy_export_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() != true)
                return (false, null, 0);

            var exportProxies = proxies.ToList();
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

            _logger.Info($"Exported {exportProxies.Count} proxies to {dialog.FileName}");
            return (true, dialog.FileName, exportProxies.Count);
        }
        catch (Exception ex)
        {
            _logger.Error($"Export failed: {ex.Message}");
            return (false, null, 0);
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <summary>
    /// 从JSON文件导入代理配置
    /// </summary>
    public (bool Success, int Count, string? Error) ImportProxies(
        IEnumerable<ProxyItem> existingProxies,
        Action<ProxyItem> addProxyCallback,
        int portRangeStart,
        int portRangeEnd)
    {
        if (IsImporting) return (false, 0, "Already importing");

        IsImporting = true;
        try
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return (false, 0, null);

            var json = File.ReadAllText(dialog.FileName, System.Text.Encoding.UTF8);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("Proxies", out var proxiesEl)
                || proxiesEl.ValueKind != JsonValueKind.Array)
            {
                return (false, 0, "Invalid export file: missing 'Proxies' array");
            }

            var usedPorts = new HashSet<int>(existingProxies.Select(p => p.LocalPort));
            var maxId = existingProxies.Any() ? existingProxies.Max(p => p.Id) : 0;
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

                    var localPort = portRangeStart;
                    while (usedPorts.Contains(localPort))
                    {
                        localPort++;
                        if (localPort > portRangeEnd) break;
                    }

                    if (localPort > portRangeEnd) break;

                    usedPorts.Add(localPort);
                    maxId++;

                    var newProxy = new ProxyItem
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
                    };

                    addProxyCallback(newProxy);
                    imported++;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Skipped invalid proxy entry during import: {ex.Message}");
                }
            }

            _logger.Info($"Imported {imported} proxies from {dialog.FileName}");
            return (true, imported, null);
        }
        catch (Exception ex)
        {
            _logger.Error($"Import failed: {ex.Message}");
            return (false, 0, ex.Message);
        }
        finally
        {
            IsImporting = false;
        }
    }
}
```

#### 步骤 2.1.5：重构 MainViewModel
```csharp
// 修改 src/YLproxy.GUI/MainViewModel.cs
// 将相关职责委托给子 ViewModel

public sealed class MainViewModel : ViewModelBase
{
    // 原有字段保持不变
    private readonly Timer _timer;
    private readonly MonitorService _monitorService;
    // ... 其他原有字段

    // 新增子 ViewModel
    public ProxyListViewModel ProxyList { get; }
    public ProxyOperationViewModel ProxyOperations { get; }
    public ImportExportViewModel ImportExport { get; }

    // 原有 Sub-ViewModels 保持不变
    public HostInfoViewModel HostInfo { get; } = new();
    public DashboardViewModel Dashboard { get; } = new();
    public LogPanelViewModel LogPanel { get; } = new();

    // 移除原有的代理集合属性，改用 ProxyList
    // public ObservableCollection<ProxyItem> Proxies { get; } = new(); // 删除
    // public ObservableCollection<ProxyItem> FilteredProxies => _filteredProxies; // 删除

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
        // 原有初始化代码
        _logger = logger;
        _settingsService = settingsService;
        _proxyConfig = proxyConfig;
        _threeProxyConfig = threeProxyConfig;
        _proxyDataService = proxyDataService;
        _proxyTester = proxyTester;
        _proxyProcessManager = proxyProcessManager;
        _apiServer = apiServer;
        _proxyProcessManager.Configure(_threeProxyConfig);

        // 新增子 ViewModel 初始化
        ProxyList = new ProxyListViewModel();
        ProxyOperations = new ProxyOperationViewModel(proxyTester, proxyProcessManager, logger);
        ImportExport = new ImportExportViewModel(logger);

        // 原有初始化代码继续
        InitFromConfig();
        LoadHostInfo();
        RefreshStats();
        AddLog($"[{DateTime.Now:HH:mm:ss}] Application started. (Phase 9 — Code Quality Improvement)");

        // 命令绑定调整
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
        ClearSearchCommand = new RelayCommand(() => ProxyList.SearchText = string.Empty);

        // ... 其他原有代码
    }

    // 修改方法使用子 ViewModel
    private void InitFromConfig()
    {
        ProxyList.ClearProxies();
        try
        {
            var cfg = _proxyDataService.Load();
            foreach (var p in cfg.Proxies)
                ProxyList.AddProxy(p);
            RefreshStats();
        }
        catch (Exception ex)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] config.json could not be loaded: {ex.Message}");
        }
    }

    private async Task TestSelectedProxyAsync()
    {
        if (SelectedProxy is null) return;

        SetStatus($"Testing {SelectedProxy.Name}...");
        var (success, latency, error) = await ProxyOperations.TestProxyAsync(SelectedProxy);

        if (success)
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Test OK — {SelectedProxy.Name}: {latency}ms");
            SetStatus($"{SelectedProxy.Name}: test passed ({latency}ms)");
        }
        else
        {
            AddLog($"[{DateTime.Now:HH:mm:ss}] Test FAILED — {SelectedProxy.Name}: {error}");
            SetStatus($"{SelectedProxy.Name}: test failed — {error}");
        }
    }

    private void StartSelectedProxy()
    {
        if (SelectedProxy is null) return;
        ProxyOperations.StartProxy(SelectedProxy);
        Application.Current?.Dispatcher.BeginInvoke(() => RefreshStats());
        AddLog($"[{DateTime.Now:HH:mm:ss}] Started: {SelectedProxy.LocalHost}:{SelectedProxy.LocalPort}");
        SetStatus($"{SelectedProxy.Name}: started on port {SelectedProxy.LocalPort}");
    }

    private void StopSelectedProxy()
    {
        if (SelectedProxy is null) return;
        ProxyOperations.StopProxy(SelectedProxy);
        Application.Current?.Dispatcher.BeginInvoke(() => RefreshStats());
        AddLog($"[{DateTime.Now:HH:mm:ss}] Stopped: {SelectedProxy.LocalHost}:{SelectedProxy.LocalPort}");
        SetStatus($"{SelectedProxy.Name}: stopped");
    }

    private void BatchStart()
    {
        var (started, failed) = ProxyOperations.BatchStart(SelectedProxies);
        Application.Current?.Dispatcher.BeginInvoke(() => RefreshStats());
        SetStatus($"Batch started: {started}/{started + failed} proxies");
    }

    private void BatchStop()
    {
        var (stopped, failed) = ProxyOperations.BatchStop(SelectedProxies);
        Application.Current?.Dispatcher.BeginInvoke(() => RefreshStats());
        SetStatus($"Batch stopped: {stopped}/{stopped + failed} proxies");
    }

    private void ExportToJson()
    {
        var (success, filePath, count) = ImportExport.ExportProxies(
            SelectedProxies.Count > 0 ? SelectedProxies : ProxyList.Proxies);

        if (success)
        {
            SetStatus($"Exported {count} proxies to {Path.GetFileName(filePath)}");
            AddLog($"[{DateTime.Now:HH:mm:ss}] Export: {count} proxies → {Path.GetFileName(filePath)}");
        }
    }

    private void ImportFromJson()
    {
        var (success, count, error) = ImportExport.ImportProxies(
            ProxyList.Proxies,
            proxy => {
                ProxyList.AddProxy(proxy);
                var cfg = _proxyDataService.Load();
                cfg.Proxies.Add(proxy);
                _proxyDataService.Save(cfg);
            },
            _proxyConfig.PortRangeStart,
            _proxyConfig.PortRangeEnd);

        if (success)
        {
            InitFromConfig();
            RefreshStats();
            SetStatus($"Imported {count} proxies");
            AddLog($"[{DateTime.Now:HH:mm:ss}] Import: {count} proxies");
        }
        else if (error != null)
        {
            SetStatus($"Import failed: {error}");
        }
    }

    // 修改 SelectedProxy 属性
    private ProxyItem? _selectedProxy;
    public ProxyItem? SelectedProxy
    {
        get => _selectedProxy;
        set => SetProperty(ref _selectedProxy, value);
    }

    // 修改 SelectedProxies 属性
    public List<ProxyItem> SelectedProxies { get; set; } = new();

    // 修改 RefreshDataGrid 方法
    private void RefreshDataGrid()
    {
        ProxyList.RefreshFilter();
        RaisePropertyChanged(nameof(ProxyList.FilteredProxies));
    }

    // 修改 RefreshStats 方法
    private void RefreshStats()
    {
        Dashboard.TotalCount = ProxyList.Proxies.Count;
        Dashboard.RunningCount = ProxyList.Proxies.Count(p => p.Status == ProxyStatus.Running);
        Dashboard.StoppedCount = ProxyList.Proxies.Count(p => p.Status == ProxyStatus.Stopped);
        Dashboard.FailedCount = ProxyList.Proxies.Count(p => p.Status == ProxyStatus.Failed);
    }
}
```

#### 步骤 2.1.6：更新 MainView.xaml 绑定
```xml
<!-- 修改 src/YLproxy.GUI/Views/MainView.xaml -->
<!-- 将原有的 Proxies 和 FilteredProxies 绑定改为 ProxyList -->

<!-- 原绑定： -->
<!-- <DataGrid ItemsSource="{Binding FilteredProxies}" ... /> -->

<!-- 新绑定： -->
<DataGrid ItemsSource="{Binding ProxyList.FilteredProxies}" ... />

<!-- 搜索框绑定： -->
<TextBox Text="{Binding ProxyList.SearchText, UpdateSourceTrigger=PropertyChanged}" ... />

<!-- 状态指示器绑定： -->
<!-- IsTesting, IsStarting, IsStopping 改为绑定到 ProxyOperations -->
<Button Command="{Binding TestCommand}" IsEnabled="{Binding ProxyOperations.IsTesting, Converter={StaticResource InverseBoolConverter}}" ... />
```

#### 步骤 2.1.7：添加单元测试
```powershell
# 创建测试文件
New-Item -Path "tests/ViewModelTests.cs" -ItemType File
```

**测试内容：**
```csharp
using Xunit;
using YLproxy.GUI.ViewModels;
using YLproxy.Models;

namespace YLproxy.Tests;

public class ViewModelTests
{
    [Fact]
    public void ProxyListViewModel_AddProxy_ShouldAddToCollection()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        var proxy = new ProxyItem { Id = 1, Name = "Test", RemoteHost = "1.2.3.4", RemotePort = 8080, LocalPort = 9000 };

        // Act
        viewModel.AddProxy(proxy);

        // Assert
        Assert.Single(viewModel.Proxies);
        Assert.Single(viewModel.FilteredProxies);
    }

    [Fact]
    public void ProxyListViewModel_SearchText_ShouldFilterProxies()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        viewModel.AddProxy(new ProxyItem { Id = 1, Name = "TestProxy", RemoteHost = "1.2.3.4", RemotePort = 8080, LocalPort = 9000 });
        viewModel.AddProxy(new ProxyItem { Id = 2, Name = "Another", RemoteHost = "5.6.7.8", RemotePort = 8080, LocalPort = 9001 });

        // Act
        viewModel.SearchText = "Test";

        // Assert
        Assert.Single(viewModel.FilteredProxies);
        Assert.Equal("TestProxy", viewModel.FilteredProxies[0].Name);
    }

    [Fact]
    public void ProxyOperationViewModel_StartProxy_ShouldUpdateStatus()
    {
        // Arrange
        var mockTester = new MockProxyTester();
        var mockManager = new MockProxyProcessManager();
        var mockLogger = new MockLogger();
        var viewModel = new ProxyOperationViewModel(mockTester, mockManager, mockLogger);
        var proxy = new ProxyItem { Id = 1, Name = "Test", RemoteHost = "1.2.3.4", RemotePort = 8080, LocalPort = 9000 };

        // Act
        viewModel.StartProxy(proxy);

        // Assert
        Assert.Equal(ProxyStatus.Running, proxy.Status);
    }
}
```

#### 步骤 2.1.8：验证和提交
```powershell
# 编译项目
dotnet build YLproxy.sln

# 运行测试
dotnet test tests/YLproxy.Tests.csproj --filter "FullyQualifiedName~ViewModel"

# 运行全部测试
dotnet test tests/YLproxy.Tests.csproj

# 提交
git add src/ tests/
git commit -m "[Phase 2.1] MainViewModel拆分 - 提取ProxyListViewModel、ProxyOperationViewModel、ImportExportViewModel"
git push origin phase2-code-quality-improvement
```

---

## 子阶段 2.2：测试覆盖率提升（78% → 82%）

### 目标
- 运行覆盖率工具生成报告
- 识别覆盖率低的模块
- 补充边缘情况测试
- 添加集成测试分类
- 目标：从 60% 提升到 80%

### 执行步骤

#### 步骤 2.2.1：安装覆盖率工具
```powershell
# 添加 Coverlet 收集器
dotnet add tests/YLproxy.Tests.csproj package coverlet.msbuild
dotnet add tests/YLproxy.Tests.csproj package coverlet.collector
```

#### 步骤 2.2.2：生成覆盖率报告
```powershell
# 运行测试并生成覆盖率报告
dotnet test tests/YLproxy.Tests.csproj --collect:"XPlat Code Coverage" --results-directory:./coverage

# 生成 HTML 报告
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage/**/coverage.cobertura.xml -targetdir:coverage/report -reporttypes:Html
```

#### 步骤 2.2.3：分析覆盖率报告
```powershell
# 打开 HTML 报告
start coverage/report/index.html

# 识别覆盖率低的模块（<70%）
# 重点关注：
# - YLproxy.Core.ProxyDataService
# - YLproxy.Proxy.ProxyProcessManager
# - YLproxy.GUI.MainViewModel
```

#### 步骤 2.2.4：补充边缘情况测试

**针对 ProxyDataService 的测试：**
```powershell
# 扩展 tests/ProxyDataServiceRecoveryTests.cs
```

**新增测试用例：**
```csharp
[Fact]
public void Load_FileLockTimeout_ShouldThrowTimeoutException()
{
    // Arrange
    var configPath = "test_lock_timeout.json";
    var logger = LoggerFactory.CreateLogger();
    
    // 模拟文件被锁定
    using var lock1 = new FileLock(configPath, timeoutMs: 10000);
    
    var service = new ProxyDataService(configPath, skipPathValidation: true, logger: logger);
    
    // Act & Assert
    Assert.Throws<TimeoutException>(() => service.Load());
}

[Fact]
public void Save_ConcurrentWrites_ShouldNotCorruptData()
{
    // Arrange
    var configPath = "test_concurrent_save.json";
    var service = new ProxyDataService(configPath, skipPathValidation: true);
    var config = new AppConfig
    {
        Proxies = new List<ProxyItem>
        {
            new() { Id = 1, Name = "Test", RemoteHost = "1.2.3.4", RemotePort = 8080, LocalPort = 9000 }
        }
    };
    
    // Act - 并发写入
    var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(() => {
        config.Proxies[0].Name = $"Test-{i}";
        service.Save(config);
    })).ToArray();
    
    Task.WaitAll(tasks);
    
    // Assert
    var loaded = service.Load();
    Assert.Single(loaded.Proxies);
    // 验证数据完整性
}
```

**针对 ProxyProcessManager 的测试：**
```powershell
# 扩展测试文件
```

#### 步骤 2.2.5：添加集成测试分类
```powershell
# 修改 tests/YLproxy.Tests.csproj
# 添加测试分类支持
```

**项目文件修改：**
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
  <PackageReference Include="xunit" Version="2.6.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
  <PackageReference Include="coverlet.msbuild" Version="6.0.0">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
  <PackageReference Include="coverlet.collector" Version="6.0.0">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

#### 步骤 2.2.6：验证覆盖率提升
```powershell
# 重新生成覆盖率报告
dotnet test tests/YLproxy.Tests.csproj --collect:"XPlat Code Coverage" --results-directory:./coverage
reportgenerator -reports:coverage/**/coverage.cobertura.xml -targetdir:coverage/report -reporttypes:Html

# 检查覆盖率是否达到 80%
start coverage/report/index.html
```

#### 步骤 2.2.7：提交
```powershell
git add tests/ YLproxy.Tests.csproj
git commit -m "[Phase 2.2] 测试覆盖率提升 - 补充边缘情况测试，覆盖率从60%提升到80%"
git push origin phase2-code-quality-improvement
```

---

## 子阶段 2.3：代码清理与优化（82% → 85%）

### 目标
- 删除 ServiceLocator.cs（改用纯 DI）
- 清理冗余 using 语句
- 统一命名规范
- 移除未使用的配置类

### 执行步骤

#### 步骤 2.3.1：删除 ServiceLocator.cs
```powershell
# 检查 ServiceLocator.cs 是否存在
Get-ChildItem -Path "src" -Recurse -Filter "ServiceLocator.cs"

# 如果存在，删除
Remove-Item src/YLproxy.GUI/ServiceLocator.cs -ErrorAction SilentlyContinue
```

#### 步骤 2.3.2：搜索 ServiceLocator 使用
```powershell
# 搜索所有引用 ServiceLocator 的地方
Select-String -Path "src\**\*.cs" -Pattern "ServiceLocator" -AllMatches
```

#### 步骤 2.3.3：替换 ServiceLocator 为 DI
```csharp
// 在 App.xaml.cs 中，如果有 ServiceLocator 使用，替换为 DI 容器直接解析
// 原代码：
// var service = ServiceLocator.Instance.GetService<ISomeService>();

// 新代码：
// var service = app.Services.GetRequiredService<ISomeService>();
```

#### 步骤 2.3.4：清理冗余 using 语句
```powershell
# 使用 dotnet format 自动清理
dotnet format YLproxy.sln

# 手动检查并清理
# 检查每个 .cs 文件，删除未使用的 using 语句
```

#### 步骤 2.3.5：统一命名规范
```powershell
# 检查命名不一致的地方
# - 私有字段：_camelCase
# - 公共属性：PascalCase
# - 方法：PascalCase
# - 常量：PascalCase
```

#### 步骤 2.3.6：移除未使用的配置类
```powershell
# 搜索未使用的配置类
Select-String -Path "src\**\*.cs" -Pattern "class.*Config" -AllMatches

# 检查每个配置类的使用情况
# 如果某个配置类没有被引用，考虑删除
```

#### 步骤 2.3.7：运行代码分析
```powershell
# 使用 Roslyn 分析器
dotnet build YLproxy.sln /warnaserror

# 修复所有警告
```

#### 步骤 2.3.8：验证和提交
```powershell
# 完整编译
dotnet build YLproxy.sln -c Release

# 运行测试
dotnet test tests/YLproxy.Tests.csproj

# 提交
git add src/
git commit -m "[Phase 2.3] 代码清理与优化 - 删除ServiceLocator、清理冗余using、统一命名规范"
git push origin phase2-code-quality-improvement
```

---

## 阶段二完成验证

### 最终验证步骤
```powershell
# 1. 完整编译
dotnet build YLproxy.sln -c Release

# 2. 运行所有测试
dotnet test tests/YLproxy.Tests.csproj

# 3. 生成覆盖率报告
dotnet test tests/YLproxy.Tests.csproj --collect:"XPlat Code Coverage" --results-directory:./coverage
reportgenerator -reports:coverage/**/coverage.cobertura.xml -targetdir:coverage/report -reporttypes:Html

# 4. 检查代码质量
dotnet format YLproxy.sln --verify-no-changes

# 5. 运行 GUI 应用测试
dotnet run --project src/YLproxy.GUI
```

### 创建合并请求
```powershell
# 切换到 main 分支
git checkout main

# 合并功能分支
git merge phase2-code-quality-improvement

# 推送到远程
git push origin main

# 删除功能分支
git branch -d phase2-code-quality-improvement
git push origin --delete phase2-code-quality-improvement
```

### 更新进度文档
```powershell
# 更新 docs/progress.md
# 添加阶段二完成记录
# 更新总体进度为 85%
```

---

## 执行记录模板

每个子阶段完成后填写：

```markdown
## [子阶段名称] 执行记录

**执行时间：** YYYY-MM-DD
**执行环境：** Windows本地
**执行人：** 本地AI

### 执行内容
- [ ] 任务1
- [ ] 任务2
- [ ] 任务3

### 遇到问题
- 问题描述1 → 解决方案
- 问题描述2 → 解决方案

### 同步记录
- Commit: [hash] [message]
- CI 状态: ✅ / ❌
- PR: #[number]

### 进度更新
- 前进度：X%
- 后进度：Y%
- 增量：+Z%
```

---

## 注意事项

1. **重构原则**：保持功能不变，仅改变结构
2. **测试先行**：重构前确保测试覆盖充分
3. **小步提交**：每个子阶段完成后立即提交
4. **向后兼容**：确保不破坏现有功能
5. **文档同步**：重构后更新相关文档
6. **性能监控**：确保重构不影响性能

---

*执行方案版本：1.0 | 创建时间：2026-07-26*
