# YLproxy 技术债偿还方案

**版本:** v1.0  
**日期:** 2026-07-19  
**基线:** build 75/75 passed, .NET 10.0

---

## 执行策略总纲

采用 **Phase B (Backbone Refactoring)** 编号，以增量替换方式逐一解决，每 Phase 完成必须通过 `dotnet build + dotnet test` 复验门槛。

```
分期策略:
  Phase B1: 修复阻塞性 P0 Bug（2 项）
  Phase B2: 接口对齐 + DI 闭环（核心架构修复）
  Phase B3: 持久化数据源统一决策
  Phase B4: MainViewModel 持续治理
  Phase B5: 安全与正确性加固
  Phase B6: 代码清理与测试补齐
```

---

## Phase B1: 修复阻塞性 P0 Bug

### B1.1 `RefreshDataGrid()` 空方法

**当前代码:** `MainViewModel.cs` L177-L178
```csharp
private void RefreshDataGrid() { }
```

**问题:** MonitorService 检测到代理退出后调用 `_refreshAction()` 不生效，DataGrid 长时间显示已不存在的 Running 状态。

**方案:** 实现 DataGrid 刷新逻辑，触发 `FilteredProxies` 集合重置和 UI 刷新。

**具体修改:**

1. 在 MainViewModel 添加 `_proxiesVersion` 计数器 + 刷新方法：
   ```csharp
   private int _proxiesVersion;
   
   private void RefreshDataGrid()
   {
       Interlocked.Increment(ref _proxiesVersion);
       // Force re-evaluation of filter + notify UI
       var current = SearchText;
       SearchText = current; // trigger ApplyProxyFilter
       OnPropertyChanged(nameof(FilteredProxies));
       OnPropertyChanged(nameof(Proxies));
   }
   ```

2. MonitorService 构造传入的 refreshAction 已经指向此方法（`MainViewModel.cs` L146），无需修改调用链。

**复验:** `dotnet test tests/YLproxy.Tests.csproj --filter "TestCategory!=E2E"` 75 pass

---

### B1.2 修复 async-over-sync 死锁风险

**当前代码:** `ProxyDataService.cs` L116, L159
```csharp
_ioLock.WaitAsync().GetAwaiter().GetResult();  // sync-over-async lock
```

**方案:** 将 `Load()` 和 `Save()` 中调用 `_ioLock` 的方式统一为同步 `Wait()`，与全方法签名（非 async）一致。

**修改:**

```csharp
// LoadFromJson:
_ioLock.Wait();  // SemaphoreSlim.Wait() is synchronous, no deadlock with WPF STA thread
try { ... }
finally { _ioLock.Release(); }

// Save:
_ioLock.Wait();
try { SaveInternal(config); }
finally { _ioLock.Release(); }
```

**注意:** `LoadAsync()` / `SaveAsync()` 保持使用 `WaitAsync()` 不变。

**复验:** 编译 + 原有测试全部通过

---

## Phase B2: 接口对齐 + DI 闭环（核心架构修复）

这是最大工作量的 Phase，涉及多个项目。

### B2.1 ProxyProcessManager 实现 IProxyProcessManager

**当前问题:** `Core/Abstractions/IProxyProcessManager.cs` 存在接口定义，但 `ProxyProcessManager` 是全静态类不实现它。

**具体方案:**

1. 创建 `ProxyProcessManager` 实例适配器：
   ```csharp
   // src/YLproxy.Proxy/ProxyProcessManagerAdapter.cs
   namespace YLproxy.Proxy;
   
   public sealed class ProxyProcessManagerAdapter : Core.Abstractions.IProxyProcessManager
   {
       public void Start(ProxyItem proxy) => ProxyProcessManager.Start(proxy);
       public void Stop(ProxyItem proxy) => ProxyProcessManager.Stop(proxy);
       public bool IsRunning(ProxyItem proxy) => ProxyProcessManager.IsRunning(proxy);
       public void EnsureDependencies() => ProxyProcessManager.Ensure3ProxyDependencies();
   }
   ```

2. 如果需要可测试性，可以进一步将 `ConcurrentDictionary` 移入实例并抽接口，但第一步适配器模式可快速解除与 MainViewModel 的紧耦合。

### B2.2 MainViewModel 使用注入的接口而非静态调用

**当前问题:** MainViewModel 通过 DI 注入了 `_proxyProcessManager` 等 4 个字段但全部未使用，代码直接调用 `YLproxy.Proxy.ProxyProcessManager.Start(p)`。

**修改计划:**

1. 将所有 `YLproxy.Proxy.ProxyProcessManager.Start(p)` 替换为 `_proxyProcessManager.Start(p)`
2. 将所有 `YLproxy.Proxy.ProxyProcessManager.Stop(p)` 替换为 `_proxyProcessManager.Stop(p)`
3. 将所有 `YLproxy.Proxy.ProxyProcessManager.IsRunning(p)` 替换为 `_proxyProcessManager.IsRunning(p)`
4. 从 `MainViewModel` 移除 `new ProxyDataService(configPath)` 的直接创建，使用注入的 `_proxyDataService`
5. 使用注入的 `_proxyTester` 替代直接 `ProxyTester.TestAsync()` 调用
6. 移除未使用的 `_proxyRepository` 字段（直到 Phase B3 决策后再用）

**涉及修改位置:**

| 当前位置 | 调用 | 替换为 |
|---------|------|--------|
| L203 | `new ProxyDataService(configPath)` | `_proxyDataService` |
| L328 | `new ProxyDataService(configPath)` | `_proxyDataService` |
| L363 | `ProxyTester.TestAsync(...)` | `_proxyTester.TestAsync(...)` |
| L411 | `ProxyProcessManager.Start(p)` | `_proxyProcessManager.Start(p)` |
| L453 | `ProxyProcessManager.Stop(p)` | `_proxyProcessManager.Stop(p)` |
| L504 | `ProxyProcessManager.Start(p)` | `_proxyProcessManager.Start(p)` |
| L544 | `ProxyProcessManager.Stop(p)` | `_proxyProcessManager.Stop(p)` |
| L649 | `new ProxyDataService(configPath)` | `_proxyDataService` |
| L728 | `ProxyProcessManager.Stop(...)` | `_proxyProcessManager.Stop(...)` |
| L730 | `ProxyProcessManager.Start(...)` | `_proxyProcessManager.Start(...)` |
| L787 | `ProxyProcessManager.Stop(...)` | `_proxyProcessManager.Stop(...)` |
| L805 | `new ProxyDataService(configPath)` | `_proxyDataService` |

### B2.3 DI 注册补齐

**App.xaml.cs 修改:**

```csharp
// 在 AddSingleton<MainViewModel> 之前添加：
services.AddSingleton<Core.Abstractions.IProxyProcessManager, ProxyProcessManagerAdapter>();
services.AddSingleton<Core.Abstractions.IProxyDataService>(sp =>
{
    var settings = sp.GetRequiredService<AppSettingsService>();
    var cfg = settings.GetConfig();
    var configPath = PathResolver.ResolvePath(cfg.Proxy.DataDirectory, cfg.Proxy.ConfigFileName);
    return new ProxyDataService(configPath);
});
services.AddSingleton<Core.Abstractions.IProxyTester, ProxyTester>();
```

**同时:** 从 MainViewModel 构造函数中移除 `new MonitorService(...)` 的直接创建，改为注入 `MonitorService`。

### B2.4 SqliteProxyRepository 实现 IProxyRepository

**修改:**

```csharp
// src/YLproxy.Core/Data/SqliteProxyRepository.cs
public sealed class SqliteProxyRepository : Abstractions.IProxyRepository, IDisposable
```

将 `GetAll()`, `GetById()`, `Add()`, `Update()`, `Delete()`, `Count()` 匹配接口签名。

### B2.5 消除静态 ServiceLocator 和 LoggerFactory

**App.xaml.cs 中:**
- 移除 `ServiceLocator.SetProvider(provider);`
- 搜索全仓 `ServiceLocator` 引用，改为从 DI 取实例

**各项目中:**
- 最终目标：所有 `LoggerFactory.CreateLogger()` 改为从 DI 注入 ILogger
- 短期：保留 LoggerFactory 但标记 `[Obsolete]`，逐步迁移

---

## Phase B3: 持久化数据源统一决策

### 决策方案

**方案 A（推荐）:** 废弃 SQLite 过渡方案，回退到纯 JSON 持久化。
- 理由: 当前 SQLite 仅用于双写，未实际使用；去掉后可消除 `DataMigrationService`、`SqliteProxyRepository`、`IProxyRepository` 的维护负担；JSON + atomic write + DPAPI/AES 加密已满足单机需求
- 删除文件: `SqliteProxyRepository.cs`, `DataMigrationService.cs`, `IProxyRepository.cs`, `.migration_completed marker`, SQLite NuGet 包
- ProxyDataService 简化为纯 JSON 操作，移除 `_sqliteRepository` 分支

**方案 B（积极）:** 完成 SQLite 切换，JSON 降级为备份。
- JSON 变更为只读备份恢复源
- 所有 CRUD 走 SQLite
- 添加迁移完成自动清理 JSON 的逻辑

**建议:** 选择方案 A。SQLite 的跨进程并发优势对单机 WPF 应用非必需，当前规模（~100 条代理）JSON 足够了。未来如需多实例共享可再引入 SQLite。

### 简化后的 ProxyDataService（方案 A）

```csharp
public sealed class ProxyDataService
{
    private static readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly ProxyDataSerializer _serializer;
    public string ConfigPath { get; }
    
    public ProxyDataService(string configPath, ISecurityService? securityService = null)
    {
        ConfigPath = /* resolve path */;
        _serializer = new ProxyDataSerializer(securityService);
    }
    
    public AppConfig Load()
    {
        _ioLock.Wait();
        try { /* read + deserialize */ }
        finally { _ioLock.Release(); }
    }
    
    public void Save(AppConfig config)
    {
        _ioLock.Wait();
        try { /* serialize + write atomically + rotate backups */ }
        finally { _ioLock.Release(); }
    }
}
```

---

## Phase B4: MainViewModel 持续治理

### 当前状态（已完成 Phase A3）

已有 3 个子 ViewModel:
- `HostInfoViewModel` - 主机信息
- `DashboardViewModel` - 统计概览
- `LogPanelViewModel` - 日志面板

### 下一步拆分

**B4.1 提取 ProxyListViewModel**

将代理列表相关逻辑（`Proxies`、`FilteredProxies`、`SelectedProxy`、`SearchText` 搜索/过滤、CRUD 命令、批量操作）提取为独立的 `ProxyListViewModel`。

```csharp
public sealed class ProxyListViewModel : ViewModelBase
{
    public ObservableCollection<ProxyItem> Proxies { get; }
    public ObservableCollection<ProxyItem> FilteredProxies { get; }
    public ProxyItem? SelectedProxy { get; set; }
    public string SearchText { get; set; }
    
    // Commands
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand TestCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand BatchStartCommand { get; }
    public RelayCommand BatchStopCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
}
```

**B4.2 提取 ImportExportViewModel**

导入/导出逻辑（`ExportToJson`, `ImportFromJson`, `IsExporting`, `IsImporting`）独立为一个服务类。

**B4.3 最终 MainViewModel 结构**

```csharp
public sealed class MainViewModel : ViewModelBase
{
    public HostInfoViewModel HostInfo { get; }
    public DashboardViewModel Dashboard { get; }
    public LogPanelViewModel LogPanel { get; }
    public ProxyListViewModel ProxyList { get; }
    
    public string StatusMessage { get; set; }
    
    // Only coordination logic left
    // 80-120 lines total
}
```

---

## Phase B5: 安全与正确性加固

### B5.1 TransparentCoalescingForwarder 异常治理

**修改:** 将空 catch 改为记录结构化日志

```csharp
catch (Exception ex)
{
    _logger.Error($"TransparentCoalescingForwarder: client handling error", ex);
}
```

### B5.2 FileSystemWatcher 线程安全

**修改:**

```csharp
private readonly ReaderWriterLockSlim _configLock = new();

public T GetSection<T>(string sectionName) where T : class, new()
{
    _configLock.EnterReadLock();
    try { /* existing switch */ }
    finally { _configLock.ExitReadLock(); }
}

private void LoadConfig()
{
    _configLock.EnterWriteLock();
    try { /* existing load logic */ }
    finally { _configLock.ExitWriteLock(); }
}
```

### B5.3 配置类移入 Models 项目

- 将 `AppSettingsService.cs` 中的 `AppSettingsConfig`, `LoggingConfig`, `ProxyConfig`, `ThreeProxyConfig`, `StartupConfig`, `ApiConfig` 移至 `src/YLproxy.Models/Config/` 目录
- Infrastructure 项目引用 Models
- 修复全仓 using 引用

### B5.4 移除 Dead Code

| 位置 | 内容 | 操作 |
|------|------|------|
| `AesSecurityService.cs` L24 | `_keyLock` 声明 | 删除 |
| `MainViewModel.cs` L35-38 | 未使用的 DI 字段（B2.2 解决后自然消除）| 验证删除 |
| `Todo` 标记 | 搜索 `// TODO:` | 逐一评估 |

---

## Phase B6: 代码清理与测试补齐

### B6.1 补充测试覆盖

**当前覆盖率缺口:**
- `ProxyDataService` 的 `RecoverFromCorruption()` 后备恢复逻辑
- `AppSettingsService` 的配置验证路径
- `MainViewModel` 的业务层（分离后可直接 mock）
- `FileLogger` 的结构化日志轮转
- `MonitorService` 的退避算法

### B6.2 添加测试分类

确保 `[TestCategory("E2E")]` 标记已正确定义，将纯单元测试与集成测试分离。

### B6.3 清理空 catch 块

全仓搜索 `catch { }` 和 `catch (Exception)` 空体，逐项评估：
- 进程清理场景 (MainViewModel L322, L787): 可保留但加注释说明
- 其他: 至少记录日志

---

## 执行时间线估算

| Phase | 内容 | 文件修改数 | 预估工时 | 风险 |
|-------|------|-----------|---------|------|
| B1.1 | RefreshDataGrid 修复 | 1 | 0.5h | 低 |
| B1.2 | async-over-sync 修复 | 1 | 0.5h | 低 |
| B2.1 | ProxyProcessManager 适配器 | 1-2 | 2h | 中 |
| B2.2 | MainViewModel 使用注入接口 | 7-10 处 | 3h | 中 |
| B2.3 | DI 注册补齐 | 2 | 1h | 低 |
| B2.4 | SqliteProxyRepository 实现接口 | 1 | 1h | 低 |
| B2.5 | ServiceLocator 消除 | 2-3 | 2h | 低 |
| B3 | 持久化方案决策 | 5-8 | 4h | 高 |
| B4 | MainViewModel 拆分 | 3-5 | 6h | 高 |
| B5.1 | Forwarder 异常治理 | 1 | 0.5h | 低 |
| B5.2 | FSW 线程安全 | 1 | 1h | 中 |
| B5.3 | 配置类迁移 | 3-5 | 2h | 中 |
| B5.4 | Dead Code 清理 | 2-3 | 1h | 低 |
| B6 | 测试补齐 | 5-10 | 6h | 中 |

**总计:** ~30h (约 1 人周)

---

## 验证门禁

每个 Phase 完成必须通过：
1. `dotnet build YLproxy.sln` - 0 Error, 0 Warning
2. `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E` - 全绿
3. 代码审查：无新空 catch、无新 TODO 引入

---

## 附录：修改清单模板

执行时可按以下格式追踪：

```
## Phase B1.1 状态
- [ ] 修改 MainViewModel.cs RefreshDataGrid()
- [ ] dotnet build 验证
- [ ] dotnet test 验证
- [ ] 提交
