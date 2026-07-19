# YLproxy 技术债清偿执行方案

**版本:** v1.0  
**日期:** 2026-07-19  
**基线:** build 75/75 passed, .NET 10.0.200  
**预计总工时:** 90 小时 (约 11 周，单人)

---

## 执行策略总纲

采用 **Phase C (Comprehensive Remediation)** 编号，按优先级分阶段清偿技术债。每个 Phase 完成必须通过 `dotnet build + dotnet test` 复验门槛。

```
分期策略:
  Phase C1: P0 阻塞性债务清偿 (1-2 周，7-11h)
  Phase C2: P1 重要性债务清偿 (2-3 周，30h)
  Phase C3: P2 优化性债务清偿 (3-4 周，46.5h)
```

---

## Phase C1: P0 阻塞性债务清偿

### C1.1 数据持久化策略决策与执行

**优先级:** 🔴 P0  
**工时:** 4-8h (取决于方案选择)  
**风险:** 高  
**状态:** ✅ 已完成（2026-07-19）——采用**方案 A（JSON-only）**。已删除
`SqliteProxyRepository.cs` / `DataMigrationService.cs` / `IProxyRepository.cs` /
`tests/SqliteMigrationTests.cs`，移除 `ProxyDataService` 与 `IProxyDataService` 中的
`MigrateToSqliteIfNeeded*` / `IsSqliteMigrated`，并清理 `Microsoft.Data.Sqlite` /
`SQLitePCLRaw` 包引用与 `NU1903` NoWarn 兜底。

#### 决策流程

1. **技术评审会议** (1h)
   - 参与者: 架构师、核心开发、产品负责人
   - 议题: JSON vs SQLite 长期架构决策
   - 决策依据:
     - 当前用户规模 (单机 < 100 代理)
     - 未来规划 (多实例共享、复杂查询)
     - 维护成本 (SQLite 迁移 + ORM + 版本化迁移)
     - 技术栈一致性 (.NET 生态 SQLite 支持成熟)

2. **方案 A: 优化 JSON 持久化** (推荐，4h)
   - **理由:** 
     - 当前单机场景 JSON 足够
     - 避免引入 SQLite 维护负担
     - 原子写入 + 备份轮转已满足可靠性
   - **执行步骤:**
     ```powershell
     # 1. 删除 SQLite 相关代码
     Remove-Item src/YLproxy.Core/Data/SqliteProxyRepository.cs
     Remove-Item src/YLproxy.Core/Data/DataMigrationService.cs
     Remove-Item src/YLproxy.Core/Abstractions/IProxyRepository.cs
     
     # 2. 清理 NuGet 引用
     # 编辑 Directory.Packages.props，移除 SQLite 相关包
     # 编辑 src/YLproxy.Core/YLproxy.Core.csproj，移除 PackageReference
     
     # 3. 简化 ProxyDataService
     # 移除 _sqliteRepository 字段
     # 移除 MigrateToSqliteIfNeeded 相关方法
     # 移除 IsSqliteMigrated 属性
     ```

   - **代码修改清单:**
     - `src/YLproxy.Core/Config/ProxyDataService.cs`
       - 删除 L175-189 (SQLite 迁移方法)
       - 删除 `_sqliteRepository` 字段引用
     - `src/YLproxy.Core/YLproxy.Core.csproj`
       - 移除 `Microsoft.Data.Sqlite` 引用
     - `Directory.Packages.props`
       - 移除 SQLitePCLRaw 相关包版本
     - `tests/YLproxy.Tests/SqliteMigrationTests.cs`
       - 删除整个测试文件

3. **方案 B: 完成 SQLite 切换** (8h)
   - **理由:**
     - 为未来多实例共享做准备
     - 支持复杂查询和统计
     - 事务支持保证数据一致性
   - **执行步骤:**
     ```powershell
     # 1. 启用迁移逻辑
     # 编辑 ProxyDataService.MigrateToSqliteIfNeeded()
     # 返回 true 并执行实际迁移
     
     # 2. 完成数据迁移
     # 读取 JSON 配置
     # 写入 SQLite 数据库
     # 标记迁移完成 (.migration_completed)
     
     # 3. JSON 降级为备份
     # 保留 JSON 读取能力用于恢复
     # 所有 CRUD 走 SQLite
     ```

   - **代码修改清单:**
     - `src/YLproxy.Core/Config/ProxyDataService.cs`
       - 实现 `MigrateToSqliteIfNeeded()` 逻辑
       - 修改 `Load()` 优先读取 SQLite
       - 修改 `Save()` 写入 SQLite
     - `src/YLproxy.Core/Data/SqliteProxyRepository.cs`
       - 实现所有 CRUD 方法
       - 添加事务支持
     - `src/YLproxy.Core/Data/DataMigrationService.cs`
       - 实现 JSON → SQLite 迁移逻辑
       - 添加迁移回滚机制

#### 验收标准

- [ ] `dotnet build YLproxy.sln` - 0 Error, 0 Warning
- [ ] `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E` - 全绿
- [ ] 数据迁移测试通过 (如选择方案 B)
- [ ] 文档更新 (`docs/progress.md`, `docs/task-tracking.md`)

#### 回滚计划

- 保留原 JSON 文件作为备份
- Git 分支保护，可随时回退
- 迁移失败时保留错误日志和原始数据

---

### C1.2 空 catch 块治理

**优先级:** 🔴 P0  
**工时:** 3h  
**风险:** 中

#### 执行步骤

1. **空 catch 块清单** (0.5h)
   - 全仓搜索确认 13 处位置
   - 分类: 合理场景 vs 需要记录日志

2. **逐个审查与修复** (2h)

   **文件: `src/YLproxy.Proxy/TransparentCoalescingForwarder.cs`**
   ```csharp
   // L265-267: 进程清理场景 - 合理，加注释
   catch
   {
       // Ignore listener stop errors during shutdown
   }
   
   // L273-275: 任务取消场景 - 合理，加注释
   catch
   {
       // Ignore wait cancellation during shutdown
   }
   
   // L280-282: 清理异常 - 记录日志
   catch (Exception ex)
   {
       _logger.Error($"Error during TransparentCoalescingForwarder shutdown: {ex.Message}", ex);
   }
   ```

   **文件: `src/YLproxy.Proxy/ManagedProxyForwarder.cs`**
   ```csharp
   // L180: Header 添加失败 - 记录日志
   catch (Exception ex)
   {
       _logger.Debug($"Failed to add header '{name}': {ex.Message}");
   }
   
   // L300: 错误响应写入失败 - 记录日志
   catch (Exception ex)
   {
       _logger.Debug($"Failed to write error response: {ex.Message}");
   }
   
   // L305-306: 进程清理场景 - 合理，加注释
   try { _cts.Cancel(); } catch { /* Ignore cancellation errors */ }
   try { _listener.Stop(); } catch { /* Ignore listener stop errors */ }
   ```

   **文件: `src/YLproxy.GUI/MainViewModel.cs`**
   ```csharp
   // L336: 代理停止失败 - 记录日志
   catch (Exception ex)
   {
       _logger.Warn($"Failed to stop proxy {proxy.Id} during removal: {ex.Message}");
   }
   
   // L709: 导入失败 - 记录日志
   catch (Exception ex)
   {
       _logger.Warn($"Failed to import proxy entry: {ex.Message}");
   }
   
   // L763: 日志写入失败 - 合理，已有注释
   // 保持现状，注释已说明意图
   
   // L800: 关闭时代理停止失败 - 记录日志
   catch (Exception ex)
   {
       _logger.Warn($"Failed to stop proxy {proxy.Id} during shutdown: {ex.Message}");
   }
   ```

   **文件: `src/YLproxy.Infrastructure/AppSettingsService.cs`**
   ```csharp
   // L140: 临时文件删除失败 - 记录日志
   catch (Exception ex)
   {
       _logger.Warn($"Failed to delete temp file '{tempPath}': {ex.Message}");
   }
   ```

   **文件: `src/YLproxy.Infrastructure/AesSecurityService.cs`**
   ```csharp
   // L83: 临时文件删除失败 - 记录日志
   catch (Exception ex)
   {
       _logger.Warn($"Failed to delete temp key file '{tempPath}': {ex.Message}");
   }
   ```

3. **验证测试** (0.5h)
   - 运行现有测试确保无破坏
   - 手动验证异常场景日志输出

#### 验收标准

- [ ] 无空 catch 块 (除明确注释说明合理场景)
- [ ] 所有异常至少记录 Warn 级别日志
- [ ] `dotnet test` 全绿
- [ ] 代码审查通过

---

## Phase C2: P1 重要性债务清偿

### C2.1 MainViewModel 继续拆分

**优先级:** 🟡 P1  
**工时:** 6h  
**风险:** 中

#### 执行步骤

1. **创建 ProxyListViewModel** (2h)
   ```csharp
   // src/YLproxy.GUI/ViewModels/ProxyListViewModel.cs
   public sealed class ProxyListViewModel : ViewModelBase
   {
       private readonly ObservableCollection<ProxyItem> _proxies = new();
       private readonly ObservableCollection<ProxyItem> _filteredProxies = new();
       private string _searchText = string.Empty;
       
       public ObservableCollection<ProxyItem> Proxies => _proxies;
       public ObservableCollection<ProxyItem> FilteredProxies => _filteredProxies;
       public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
       
       // Commands
       public RelayCommand AddCommand { get; }
       public RelayCommand EditCommand { get; }
       public RelayCommand RemoveCommand { get; }
       public RelayCommand ClearSearchCommand { get; }
       
       private void ApplyProxyFilter() { /* 从 MainViewModel 迁移 */ }
   }
   ```

2. **创建 ProxyOperationViewModel** (2h)
   ```csharp
   // src/YLproxy.GUI/ViewModels/ProxyOperationViewModel.cs
   public sealed class ProxyOperationViewModel : ViewModelBase
   {
       private bool _isTesting;
       private bool _isStarting;
       private bool _isStopping;
       
       public bool IsTesting { get => _isTesting; set => SetProperty(ref _isTesting, value); }
       public bool IsStarting { get => _isStarting; set => SetProperty(ref _isStarting, value); }
       public bool IsStopping { get => _isStopping; set => SetProperty(ref _isStopping, value); }
       
       // Commands
       public RelayCommand TestCommand { get; }
       public RelayCommand StartCommand { get; }
       public RelayCommand StopCommand { get; }
       public RelayCommand BatchStartCommand { get; }
       public RelayCommand BatchStopCommand { get; }
       
       // 操作逻辑从 MainViewModel 迁移
   }
   ```

3. **创建 ImportExportViewModel** (1h)
   ```csharp
   // src/YLproxy.GUI/ViewModels/ImportExportViewModel.cs
   public sealed class ImportExportViewModel : ViewModelBase
   {
       private bool _isExporting;
       private bool _isImporting;
       
       public bool IsExporting { get => _isExporting; set => SetProperty(ref _isExporting, value); }
       public bool IsImporting { get => _isImporting; set => SetProperty(ref _isImporting, value); }
       
       // Commands
       public RelayCommand ExportCommand { get; }
       public RelayCommand ImportCommand { get; }
       
       // 导入导出逻辑从 MainViewModel 迁移
   }
   ```

4. **重构 MainViewModel** (1h)
   ```csharp
   // src/YLproxy.GUI/MainViewModel.cs
   public sealed class MainViewModel : ViewModelBase
   {
       // 子 ViewModel
       public HostInfoViewModel HostInfo { get; } = new();
       public DashboardViewModel Dashboard { get; } = new();
       public LogPanelViewModel LogPanel { get; } = new();
       public ProxyListViewModel ProxyList { get; } = new();
       public ProxyOperationViewModel ProxyOperation { get; } = new();
       public ImportExportViewModel ImportExport { get; } = new();
       
       // 仅保留协调逻辑
       // 目标: < 150 行
   }
   ```

5. **更新 XAML 绑定** (0.5h)
   ```xml
   <!-- Views/MainView.xaml -->
   <!-- 更新绑定路径 -->
   <TextBox Text="{Binding ProxyList.SearchText}" />
   <Button Command="{Binding ProxyList.AddCommand}" />
   <Button Command="{Binding ProxyOperation.TestCommand}" />
   ```

6. **验证测试** (0.5h)
   - 编译验证
   - 手动 UI 测试

#### 验收标准

- [ ] MainViewModel < 150 行
- [ ] `dotnet build` 通过
- [ ] `dotnet test` 全绿
- [ ] UI 功能正常

---

### C2.2 CI/CD 发布自动化

**优先级:** 🟡 P1  
**工时:** 6h  
**风险:** 中

#### 执行步骤

1. **创建 release.yml workflow** (2h)
   ```yaml
   # .github/workflows/release.yml
   name: Release
   
   on:
     push:
       tags:
         - 'v*'
   
   jobs:
     release:
       runs-on: windows-latest
       steps:
         - uses: actions/checkout@v4
         
         - name: Setup .NET
           uses: actions/setup-dotnet@v4
           with:
             global-json-file: global.json
         
         - name: Prepare 3proxy runtime
           run: pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\prepare-runtime.ps1
         
         - name: Build
           run: dotnet build YLproxy.sln -c Release --warnaserror
         
         - name: Test
           run: dotnet test tests/YLproxy.Tests.csproj -c Release --filter TestCategory!=E2E
         
         - name: Publish
           run: dotnet publish src/YLproxy.GUI -c Release -r win-x64 --self-contained true -o publish/YLproxy
         
         - name: Package
           run: |
             Compress-Archive -Path publish/YLproxy/* -DestinationPath YLproxy-win-x64.zip
         
         - name: Generate SBOM
           run: dotnet tool run dotnet-CycloneDX YLproxy.sln -o sbom.json
         
         - name: Create Release
           uses: softprops/action-gh-release@v1
           with:
             files: |
               YLproxy-win-x64.zip
               sbom.json
           env:
             GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
   ```

2. **配置版本管理** (1h)
   ```json
   // .version 文件
   0.2.0
   ```
   
   ```powershell
   # scripts/update-version.ps1
   $version = Get-Content .version
   # 更新 AssemblyInfo.cs
   # 更新 README.md
   ```

3. **集成 CycloneDX** (1h)
   ```bash
   # 安装工具
   dotnet tool install --global CycloneDX
   
   # 本地测试
   dotnet-CycloneDX YLproxy.sln -o sbom.json
   ```

4. **测试发布流程** (1h)
   - 创建测试 tag
   - 验证自动发布
   - 检查产物完整性

5. **文档更新** (1h)
   - 更新 `docs/deployment.md`
   - 添加发布流程说明

#### 验收标准

- [ ] tag 触发自动发布
- [ ] Release 包含 exe + 3proxy + SBOM
- [ ] 发布流程可手动验证

---

### C2.3 FileSystemWatcher 线程安全

**优先级:** 🟡 P1  
**工时:** 2h  
**风险:** 中

#### 执行步骤

1. **引入读写锁** (0.5h)
   ```csharp
   // src/YLproxy.Infrastructure/AppSettingsService.cs
   private readonly ReaderWriterLockSlim _configLock = new();
   ```

2. **保护读取路径** (0.5h)
   ```csharp
   public T GetSection<T>(string sectionName) where T : class, new()
   {
       _configLock.EnterReadLock();
       try
       {
           return sectionName switch
           {
               "Logging" => _config.Logging as T ?? new T(),
               "Proxy" => _config.Proxy as T ?? new T(),
               "ThreeProxy" => _config.ThreeProxy as T ?? new T(),
               "Api" => _config.Api as T ?? new T(),
               _ => new T()
           };
       }
       finally
       {
           _configLock.ExitReadLock();
       }
   }
   
   public AppSettingsConfig GetConfig()
   {
       _configLock.EnterReadLock();
       try
       {
           return _config;
       }
       finally
       {
           _configLock.ExitReadLock();
       }
   }
   ```

3. **保护写入路径** (0.5h)
   ```csharp
   private void LoadConfig()
   {
       _configLock.EnterWriteLock();
       try
       {
           // existing load logic
       }
       finally
       {
           _configLock.ExitWriteLock();
       }
   }
   
   private void OnConfigChanged(object sender, FileSystemEventArgs e)
   {
       _configLock.EnterWriteLock();
       try
       {
           LoadConfig();
       }
       finally
       {
           _configLock.ExitWriteLock();
       }
   }
   ```

4. **压力测试** (0.5h)
   - 多线程并发读写测试
   - 验证无死锁

#### 验收标准

- [ ] 多线程压力测试通过
- [ ] `dotnet test` 全绿
- [ ] 无死锁现象

---

### C2.4 测试覆盖率提升

**优先级:** 🟡 P1  
**工时:** 16h  
**风险:** 中

#### 执行步骤

1. **补充核心业务逻辑单元测试** (8h)
   ```csharp
   // tests/YLproxy.Tests/ProxyDataServiceCoverageTests.cs
   public class ProxyDataServiceCoverageTests
   {
       [Fact]
       public void RecoverFromCorruption_ShouldRestoreFromBackup()
       {
           // 测试备份恢复逻辑
       }
       
       [Fact]
       public void RotateBackups_ShouldMaintainMaxCount()
       {
           // 测试备份轮转
       }
   }
   
   // tests/YLproxy.Tests/MonitorServiceCoverageTests.cs
   public class MonitorServiceCoverageTests
   {
       [Fact]
       public void ExponentialBackoff_ShouldIncreaseDelay()
       {
           // 测试退避算法
       }
   }
   ```

2. **添加集成测试分类** (4h)
   ```csharp
   [Trait("Category", "Integration")]
   public class ProxyIntegrationTests
   {
       [Fact]
       public async Task RealProxy_ShouldConnect()
       {
           // 真实代理集成测试
       }
   }
   ```

3. **引入 FlaUI UI 自动化** (4h)
   ```bash
   # 安装 FlaUI
   dotnet add package FlaUI.UIA3
   
   # 创建 UI 测试
   # tests/YLproxy.Tests/UI/AddProxyE2ETests.cs
   ```

4. **配置覆盖率收集** (0.5h)
   ```xml
   <!-- Directory.Build.props -->
   <PropertyGroup>
     <CollectCoverage>true</CollectCoverage>
     <CoverletOutputFormat>opencover</CoverletOutputFormat>
   </PropertyGroup>
   ```

5. **设置覆盖率目标** (0.5h)
   ```yaml
   # .github/workflows/ci.yml
   - name: Test with Coverage
     run: dotnet test --collect:"XPlat Code Coverage"
   
   - name: Upload Coverage
     uses: codecov/codecov-action@v4
   ```

#### 验收标准

- [ ] 覆盖率 ≥ 80%
- [ ] 集成测试正确分类
- [ ] UI 自动化测试可运行
- [ ] CI 中集成覆盖率报告

---

## Phase C3: P2 优化性债务清偿

### C3.1 本地 REST API 实现

**优先级:** 🟢 P2  
**工时:** 12h  
**风险:** 低

#### 执行步骤

1. **创建 API 项目** (2h)
   ```bash
   dotnet new webapi -src YLproxy.Api
   ```

2. **实现代理 CRUD 端点** (4h)
   ```csharp
   // src/YLproxy.Api/Endpoints/ProxyEndpoints.cs
   app.MapGet("/api/v1/proxies", async (IProxyDataService service) => 
   {
       var config = await service.LoadAsync();
       return Results.Ok(config.Proxies);
   });
   
   app.MapPost("/api/v1/proxies", async (ProxyItem proxy, IProxyDataService service) => 
   {
       // 创建代理逻辑
   });
   
   app.MapPut("/api/v1/proxies/{id}", async (int id, ProxyItem proxy, IProxyDataService service) => 
   {
       // 更新代理逻辑
   });
   
   app.MapDelete("/api/v1/proxies/{id}", async (int id, IProxyDataService service) => 
   {
       // 删除代理逻辑
   });
   ```

3. **实现操作端点** (3h)
   ```csharp
   app.MapPost("/api/v1/proxies/{id}/test", async (int id, IProxyTester tester) => 
   {
       // 测试代理逻辑
   });
   
   app.MapPost("/api/v1/proxies/{id}/start", async (int id, IProxyProcessManager manager) => 
   {
       // 启动代理逻辑
   });
   
   app.MapPost("/api/v1/proxies/{id}/stop", async (int id, IProxyProcessManager manager) => 
   {
       // 停止代理逻辑
   });
   ```

4. **添加 API Token 认证** (2h)
   ```csharp
   app.UseMiddleware<ApiTokenMiddleware>();
   
   // src/YLproxy.Api/Middleware/ApiTokenMiddleware.cs
   public class ApiTokenMiddleware
   {
       public async Task InvokeAsync(HttpContext context, IOptions<ApiConfig> config)
       {
           var token = context.Request.Headers["Authorization"].FirstOrDefault();
           if (token != $"Bearer {config.Value.ApiToken}")
           {
               context.Response.StatusCode = 401;
               return;
           }
           await _next(context);
       }
   }
   ```

5. **集成到主应用** (1h)
   ```csharp
   // App.xaml.cs
   // 启动 API 服务器
   var apiTask = WebApplication.CreateBuilder()
       .Build()
       .RunAsync();
   ```

#### 验收标准

- [ ] API 可通过 Postman 验证
- [ ] API Token 认证正常
- [ ] `dotnet test` 全绿

---

### C3.2 结构化审计日志

**优先级:** 🟢 P2  
**工时:** 6h  
**风险:** 低

#### 执行步骤

1. **创建审计日志模型** (1h)
   ```csharp
   // src/YLproxy.Models/AuditLogEntry.cs
   public class AuditLogEntry
   {
       public int Id { get; set; }
       public DateTime Timestamp { get; set; }
       public string Action { get; set; }
       public string TargetType { get; set; }
       public int? TargetId { get; set; }
       public string TargetName { get; set; }
       public string Detail { get; set; }
       public string Result { get; set; }
       public string ErrorMessage { get; set; }
   }
   ```

2. **创建 AuditLogService** (2h)
   ```csharp
   // src/YLproxy.Core/AuditLogService.cs
   public class AuditLogService
   {
       public void Log(string action, string targetType, int? targetId, string targetName, 
                       string result, string? errorMessage = null)
       {
           var entry = new AuditLogEntry
           {
               Timestamp = DateTime.UtcNow,
               Action = action,
               TargetType = targetType,
               TargetId = targetId,
               TargetName = targetName,
               Result = result,
               ErrorMessage = errorMessage
           };
           
           // 写入审计日志文件或数据库
       }
       
       public IEnumerable<AuditLogEntry> Query(DateTime? start = null, DateTime? end = null)
       {
           // 查询审计日志
       }
   }
   ```

3. **集成到关键操作** (2h)
   ```csharp
   // MainViewModel.cs
   private async Task StartSelectedProxy()
   {
       _auditLog.Log("proxy_start", "proxy", proxy.Id, proxy.Name, "success");
       // ... 启动逻辑
   }
   ```

4. **添加审计日志 UI** (1h)
   - 在主窗口添加审计日志查看按钮
   - 显示审计日志列表
   - 支持筛选和导出

#### 验收标准

- [ ] 关键操作有审计记录
- [ ] 审计日志可查询导出
- [ ] `dotnet test` 全绿

---

### C3.3 配置类迁移

**优先级:** 🟢 P2  
**工时:** 2h  
**风险:** 低

#### 执行步骤

1. **创建 Models/Config 目录** (0.5h)
   ```bash
   mkdir src/YLproxy.Models/Config
   ```

2. **迁移配置类** (1h)
   ```csharp
   // src/YLproxy.Models/Config/AppSettingsConfig.cs
   namespace YLproxy.Models.Config;
   
   public class AppSettingsConfig
   {
       public LoggingConfig Logging { get; set; } = new();
       public ProxyConfig Proxy { get; set; } = new();
       public ThreeProxyConfig ThreeProxy { get; set; } = new();
       public ApiConfig Api { get; set; } = new();
   }
   
   // 同样迁移其他配置类
   ```

3. **更新引用** (0.5h)
   ```csharp
   // src/YLproxy.Infrastructure/AppSettingsService.cs
   using YLproxy.Models.Config;
   
   // 更新所有 using 语句
   ```

#### 验收标准

- [ ] 编译通过
- [ ] 依赖方向正确 (Infrastructure → Models)
- [ ] `dotnet test` 全绿

---

### C3.4 3proxy 配置模板化

**优先级:** 🟢 P2  
**工时:** 4h  
**风险:** 低

#### 执行步骤

1. **引入 Scriban** (0.5h)
   ```bash
   dotnet add package Scriban
   ```

2. **创建模板文件** (1.5h)
   ```scriban
   {{- comment "3proxy configuration template" -}}
   {{- comment "Generated by YLproxy" -}}
   
   setgid 0
   setuid 0
   
   {{ if proxy.RequiresAuth }}
   auth cache strong
   users {{ proxy.Username }}:CL:{{ proxy.Password }}
   {{ end }}
   
   parent 1000 {{ proxy.RemoteHost }} {{ proxy.RemotePort }} {{ if proxy.RequiresAuth }}{{ proxy.Username }} {{ proxy.Password }}{{ end }}
   fakeresolve
   
   allow *
   proxy -s -n -r -a -p{{ proxy.LocalPort }}
   
   log {{ logPath }} D
   ```

3. **重构 ConfigGenerator** (1.5h)
   ```csharp
   // src/YLproxy.Proxy/ConfigGenerator.cs
   public static string Generate(ProxyItem proxy)
   {
       var templatePath = Path.Combine("Templates", "proxy.conf.sbncs");
       var template = Template.Parse(File.ReadAllText(templatePath));
       
       var context = new
       {
           proxy = proxy,
           logPath = GetLogPath(proxy)
       };
       
       return template.Render(context);
   }
   ```

4. **测试验证** (0.5h)
   - 单元测试验证生成结果
   - 对比原有字符串拼接输出

#### 验收标准

- [ ] 配置生成逻辑可测试
- [ ] 模板文件可独立维护
- [ ] `dotnet test` 全绿

---

### C3.5 清理冗余代码

**优先级:** 🟢 P2  
**工时:** 0.5h  
**风险:** 低

#### 执行步骤

1. **删除 ServiceLocator.cs** (0.25h)
   ```bash
   Remove-Item src/YLproxy.GUI/ServiceLocator.cs
   ```

2. **确认无引用** (0.25h)
   ```bash
   # 全仓搜索 ServiceLocator
   # 确认无引用后删除
   ```

#### 验收标准

- [ ] 编译通过
- [ ] 无 ServiceLocator 引用

---

### C3.6 性能测试建立

**优先级:** 🟢 P2  
**工时:** 8h  
**风险:** 低

#### 执行步骤

1. **引入 BenchmarkDotNet** (1h)
   ```bash
   dotnet add package BenchmarkDotNet
   ```

2. **创建性能基准测试** (4h)
   ```csharp
   // tests/YLproxy.Tests/Benchmarks/ProxyDataSerializerBenchmarks.cs
   [MemoryDiagnoser]
   public class ProxyDataSerializerBenchmarks
   {
       private AppConfig _config;
       
       [GlobalSetup]
       public void Setup()
       {
           _config = new AppConfig
           {
               Proxies = Enumerable.Range(0, 100)
                   .Select(i => new ProxyItem { /* ... */ })
                   .ToList()
           };
       }
       
       [Benchmark]
       public string Serialize()
       {
           var serializer = new ProxyDataSerializer();
           return serializer.Serialize(_config);
       }
       
       [Benchmark]
       public AppConfig Deserialize()
       {
           var serializer = new ProxyDataSerializer();
           var json = serializer.Serialize(_config);
           return serializer.Deserialize(json, out _);
       }
   }
   ```

3. **集成 CI** (2h)
   ```yaml
   # .github/workflows/performance.yml
   name: Performance
   
   on:
     push:
       branches: [ main ]
   
   jobs:
     benchmark:
       runs-on: windows-latest
       steps:
         - uses: actions/checkout@v4
         - name: Run Benchmarks
           run: dotnet run -c Release --project tests/YLproxy.Tests/Benchmarks
   ```

4. **建立基线** (1h)
   - 运行基准测试
   - 记录基线数据
   - 文档化性能目标

#### 验收标准

- [ ] 性能基准可运行
- [ ] CI 中集成性能测试
- [ ] 基线数据已记录

---

### C3.7 API 文档生成

**优先级:** 🟢 P2  
**工时:** 2h  
**风险:** 低

#### 执行步骤

1. **集成 Swashbuckle** (1h)
   ```bash
   dotnet add package Swashbuckle.AspNetCore
   ```

2. **配置 Swagger** (0.5h)
   ```csharp
   // src/YLproxy.Api/Program.cs
   builder.Services.AddEndpointsApiExplorer();
   builder.Services.AddSwaggerGen();
   
   app.UseSwagger();
   app.UseSwaggerUI();
   ```

3. **添加 XML 注释** (0.5h)
   ```xml
   <!-- src/YLproxy.Api/YLproxy.Api.csproj -->
   <PropertyGroup>
     <GenerateDocumentationFile>true</GenerateDocumentationFile>
   </PropertyGroup>
   ```

#### 验收标准

- [ ] /swagger 端点可访问
- [ ] API 文档完整显示

---

### C3.8 国际化支持

**优先级:** 🟢 P2  
**工时:** 12h  
**风险:** 低

#### 执行步骤

1. **创建资源文件** (4h)
   ```xml
   <!-- src/YLproxy.GUI/Strings/Resources.resx -->
   <data name="MainWindow_Title" xml:space="preserve">
     <value>YLproxy - 本地代理转换管理程序</value>
   </data>
   
   <!-- src/YLproxy.GUI/Strings/Resources.en.resx -->
   <data name="MainWindow_Title" xml:space="preserve">
     <value>YLproxy - Local Proxy Conversion Manager</value>
   </data>
   ```

2. **抽取 UI 字符串** (6h)
   - 遍历所有 XAML 文件
   - 替换硬编码字符串为资源引用
   - 更新 C# 代码中的字符串

3. **实现语言切换** (2h)
   ```csharp
   // src/YLproxy.GUI/Services/CultureService.cs
   public class CultureService
   {
       public void SetCulture(string culture)
       {
           var cultureInfo = new CultureInfo(culture);
           CultureInfo.CurrentCulture = cultureInfo;
           CultureInfo.CurrentUICulture = cultureInfo;
       }
   }
   ```

#### 验收标准

- [ ] 中英文切换正常
- [ ] 所有 UI 字符串已抽取
- [ ] 资源文件完整

---

## 验收门禁

每个 Phase 完成必须通过:

1. **编译门禁:** `dotnet build YLproxy.sln` - 0 Error, 0 Warning
2. **测试门禁:** `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E` - 全绿
3. **代码审查:** 无新空 catch (除明确注释)，无新 TODO
4. **文档同步:** 更新 `docs/progress.md`, `docs/task-tracking.md`, `docs/changelog.md`

---

## 执行检查清单

### Phase C1 检查清单

- [ ] C1.1: 数据持久化策略决策完成
- [ ] C1.1: 方案执行完成 (JSON 优化 或 SQLite 切换)
- [ ] C1.1: 验收测试通过
- [ ] C1.2: 空 catch 块审查完成
- [ ] C1.2: 所有 catch 块已修复或注释
- [ ] C1.2: 验收测试通过
- [ ] Phase C1 文档更新完成

### Phase C2 检查清单

- [ ] C2.1: ProxyListViewModel 创建完成
- [ ] C2.1: ProxyOperationViewModel 创建完成
- [ ] C2.1: ImportExportViewModel 创建完成
- [ ] C2.1: MainViewModel 重构完成 (< 150 行)
- [ ] C2.1: XAML 绑定更新完成
- [ ] C2.1: 验收测试通过
- [ ] C2.2: release.yml workflow 创建完成
- [ ] C2.2: 版本管理配置完成
- [ ] C2.2: CycloneDX 集成完成
- [ ] C2.2: 发布流程测试通过
- [ ] C2.3: FileSystemWatcher 线程安全加固完成
- [ ] C2.3: 压力测试通过
- [ ] C2.3: 验收测试通过
- [ ] C2.4: 单元测试补充完成
- [ ] C2.4: 集成测试分类完成
- [ ] C2.4: FlaUI UI 自动化引入完成
- [ ] C2.4: 覆盖率 ≥ 80%
- [ ] C2.4: CI 覆盖率集成完成
- [ ] Phase C2 文档更新完成

### Phase C3 检查清单

- [ ] C3.1: REST API 项目创建完成
- [ ] C3.1: CRUD 端点实现完成
- [ ] C3.1: 操作端点实现完成
- [ ] C3.1: API Token 认证完成
- [ ] C3.1: API 集成到主应用完成
- [ ] C3.1: 验收测试通过
- [ ] C3.2: 审计日志模型创建完成
- [ ] C3.2: AuditLogService 创建完成
- [ ] C3.2: 关键操作集成完成
- [ ] C3.2: 审计日志 UI 完成完成
- [ ] C3.2: 验收测试通过
- [ ] C3.3: 配置类迁移完成
- [ ] C3.3: 引用更新完成
- [ ] C3.3: 验收测试通过
- [ ] C3.4: Scriban 引入完成
- [ ] C3.4: 模板文件创建完成
- [ ] C3.4: ConfigGenerator 重构完成
- [ ] C3.4: 验收测试通过
- [ ] C3.5: ServiceLocator 删除完成
- [ ] C3.5: 无引用确认完成
- [ ] C3.5: 验收测试通过
- [ ] C3.6: BenchmarkDotNet 引入完成
- [ ] C3.6: 性能基准测试创建完成
- [ ] C3.6: CI 集成完成
- [ ] C3.6: 基线数据记录完成
- [ ] C3.7: Swashbuckle 集成完成
- [ ] C3.7: Swagger 配置完成
- [ ] C3.7: XML 注释添加完成
- [ ] C3.7: 验收测试通过
- [ ] C3.8: 资源文件创建完成
- [ ] C3.8: UI 字符串抽取完成
- [ ] C3.8: 语言切换实现完成
- [ ] C3.8: 验收测试通过
- [ ] Phase C3 文档更新完成

---

## 总结

本执行方案提供了 YLproxy 项目技术债清偿的详细路径，按优先级分为三个 Phase，预计总工时 90 小时。建议立即启动 Phase C1，优先解决阻塞性 P0 债务，为后续开发奠定稳定基础。

**关键成功因素:**
1. 数据持久化策略需要技术评审，确保长期架构正确性
2. 测试覆盖率提升需要持续投入，建立质量门禁
3. CI/CD 自动化将显著提升发布效率和可靠性
4. 文档同步确保技术决策可追溯

**建议下一步:**
立即召开技术评审会议，决策数据持久化策略 (C1.1)，随后启动空 catch 块治理 (C1.2)。
