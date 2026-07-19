## 文档纠偏：真实落地现状核查（2026-07-19）

### 变更
- 按「代码即真相」重新核查当前分支（含最新 main），在 `docs/development/progress.md` 顶部新增权威**「真实落地现状快照」**：区分 ✅ 已落地 / 👻 幽灵未接线（`YLproxy.Api`、`TransparentCoalescingForwarder`、`AesSecurityService`、重复 `AutoStartService`）/ ⛔ 未实现（Job Object 孤儿进程防护）/ 🟡 部分完成（`MainViewModel` 约 841 行）/ ⚠️ 安全遗留。
- 更正 `progress.md` 与 `docs/pending/task-tracking.md` 中易误读为"✅ 已完成"的 Job Object 条目；作废 P2 中的 SQLite 子项（Phase C1 已定 JSON-only）；作废 B2 记录里的 `SqliteProxyRepository` 条目。
- `TODO.md` 新增「下一步优先级」清单（P0 凭据轮换/历史清理、P1 编辑端口 Bug + Job Object 防护 + cfg 明文最小化、P2 死代码清理 + API 去留决策 + MainViewModel 瘦身 + 日志治理）。

### 说明
- 纯文档纠偏，不改动任何生产代码/构建/CI。

## Phase C1 P0 补丁：CI quality-gate 修复 + 分析器告警清偿（2026-07-19）

### 变更
- **分析器告警（`-warnaserror`）真修，不再宽泛抑制：** 修复 CA1305（`int.ToString` 指定 `CultureInfo.InvariantCulture`）、CA1512（`ArgumentOutOfRangeException.ThrowIf*`）、CA1869（缓存 `JsonSerializerOptions` 静态实例）、CA1507（`"Proxies"` 提取为具名常量）、CA1001（`MainViewModel`/`MainWindow`/`AppSettingsService`/`ApiServer` 及相关测试实现 `IDisposable`）、CS0168（测试 catch 未用 `ex`）。
- **`Directory.Build.props`：** 将上游合入的宽泛 `NoWarn`（CA1805;CA1512;CA1716;CA1869;CA1311;CA1304;CA1305;CA1001;CA1000）收敛为**仅** `CA1716`，并移至无条件 PropertyGroup 使 Debug/Release `-warnaserror` 均生效。CA1716 针对跨语言公共库的保留字命名（`Error`/`Stop`），YLproxy 为内部 App 接口、规则不适用，故作用域抑制并注释说明；其余告警均在代码中修复。
- **CI：** `global.json` `rollForward` 对齐 `latestPatch`（与 `scripts/validate-workspace.ps1` 校验一致）；`scripts/validate-workspace.ps1` 工作区路径解析取上游修复版（相对 workspace 文件目录解析，兼容根目录 `ci.code-workspace`）。
- 合入 `origin/main`（含 PR #17 配置对齐），冲突按 JSON-only 决策与本补丁口径解决。

### 验证
- `dotnet build YLproxy.sln --configuration Debug -warnaserror`：Build succeeded，0 Warning，0 Error。
- `dotnet build YLproxy.sln --configuration Release -warnaserror`：Build succeeded，0 Warning，0 Error。
- `dotnet test tests/YLproxy.Tests.csproj --configuration Debug --filter "Category=Unit"`：Passed 10，Failed 0。
- SDK：本地 10.0.204（band-200，符合 `global.json` latestPatch 特性带）。

### 遗留 / 后续
- CA1716 若日后 YLproxy 演进为对外公共库，应改为重命名 `Error`/`Stop` 成员而非抑制。
- `AddProxyViewModel` 编辑模式自身端口误判为「已占用」的**既存** Bug 仍未修（未授权，独立处理）。

## Phase C1 P0 债务清偿（2026-07-19）

### 变更
- **安全：** 删除 `tests/Program.cs`（已排除编译的手动控制台脚本），其中硬编码了真实上游代理主机与明文账号/密码；同步移除 `tests/YLproxy.Tests.csproj` 的 `<Compile Remove="Program.cs" />`，重写 `tests/README.md`。
- **持久化决策（方案 A：JSON-only）：** 删除未接线的 SQLite 层 `SqliteProxyRepository.cs` / `DataMigrationService.cs` / `IProxyRepository.cs` / `tests/SqliteMigrationTests.cs`；移除 `IProxyDataService`/`ProxyDataService` 的 `MigrateToSqliteIfNeeded*`、`IsSqliteMigrated` 及对应测试；清理 `Microsoft.Data.Sqlite`/`SQLitePCLRaw` 包引用与 `Directory.Build.props` 的 `NU1903` NoWarn 兜底。
- **文档纠偏：** 更正 `TODO.md`（B5 Job Object 未实现、A3 拆分部分完成、B3 已决策）、`TODO_PHASEA*.md`、`README.md`（目录树补 `YLproxy.Api`/`ManagedProxyForwarder`、SDK 基线对齐 `global.json` 10.0.200、去除 Phase 7/8 口径漂移）、`docs/incomplete/*`、`docs/pending/debt-remediation-execution-plan-20260719.md`（C1.1 完成）。
- **工程卫生：** 从 git 跟踪移除 `build.binlog`、`build_stdout.txt`、`tests/TestResults/`、`test_3proxy.cfg`，加固 `.gitignore`。

### 验证
- `dotnet build YLproxy.sln`：Build succeeded，0 Error，36 Warning（均为基线既存 CA 警告）。
- `dotnet test tests/YLproxy.Tests.csproj --filter "Category=Unit"`：Passed 10，Failed 0（CI 默认门禁子集）。
- `dotnet build YLproxy.sln -c Release -warnaserror`：本地 SDK 10.0.302 下暴露 1 处**既存** CA1507（`MainViewModel.cs:652`，JSON 属性名字符串，nameof 不适用），非本次改动引入；`global.json` 锁定的 CI SDK（10.0.200）不报此项。

### 遗留 / 后续
- ⚠️ 泄露的上游代理账号/密码需**人工在服务商侧轮换**（无法在仓内完成）。
- Job Object 孤儿进程防护未实现，已列为独立后续任务（TODO B5-new）。
- MainViewModel 协调器瘦身待续（TODO B4）。

## Phase A3：子 ViewModel 组合模式（2026-07-19）

### 变更
- `src/YLproxy.GUI/MainViewModel.cs`：引入 `HostInfoViewModel`、`DashboardViewModel`、`LogPanelViewModel` 三个子 ViewModel 作为协调器属性，移除内联的 12 个重复属性。
- `src/YLproxy.GUI/Views/MainView.xaml.cs`：`CollectionChanged` 订阅从 `_subscribedVm.FilteredLogs` 改为 `_subscribedVm.LogPanel.FilteredLogs`。
- `src/YLproxy.GUI/Views/MainView.xaml`：修复 Button 缺少 `/>` 的 XAML 语法错误。
- `src/YLproxy.GUI/ViewModels/LogPanelViewModel.cs`：修复 `SetProperty` 返回 `void` 编译错误。

### 验证
- `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
- `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## Phase A4：ProxyItem.CreateTime init-only 化（2026-07-19）

### 变更
- `src/YLproxy.Models/ProxyItem.cs`：`CreateTime { get; set; }` → `CreateTime { get; init; }`，防止创建后篡改。

### 验证
- `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
- `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## Phase A2：接口抽取（AppSettings/IProxy... 接口契约对齐）（2026-07-18）

### 变更
- `src/YLproxy.Infrastructure/IAppSettingsService.cs`：接口契约对齐 `AppSettingsConfig` 返回类型。
- `src/YLproxy.Infrastructure/AppSettingsService.cs`：修复 `GetConfig()` 返回类型并确保 `AppSettingsConfig` 相关定义可用。

### 验证
- `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
- `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## Phase A1：DI 注册 + MainViewModel 构造链闭合（2026-07-18）

### 变更
- `src/YLproxy.GUI/App.xaml.cs`：补齐 DI 注册并通过 DI 创建 `MainViewModel`（启动链闭合）。
- `src/YLproxy.GUI/MainViewModel.cs`：无参构造迁移为依赖注入构造。

### 验证
- `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
- `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## v0.3.0 (2026-07-16)


### 新增
- SQLite 数据持久化层（SqliteProxyRepository）
- JSON → SQLite 自动迁移（DataMigrationService）
- 双写过渡期策略（JSON + SQLite 并行）
- Windows 服务安装/卸载脚本
- 发布打包脚本
- 日志生命周期策略文档化
- 3proxy 引擎日志保留策略

### 变更
- ProxyProcessManager 从 Console 输出迁移到 ILogger
- AppSettingsService 从 Console 输出迁移到 ILogger
- TransparentCoalescingForwarder 从 Console 输出迁移到 ILogger
- ProxyDataService 从 Console 输出迁移到 ILogger
- FileLogger 清理错误现在可通过 CleanupErrors 集合查询
- 空 catch 块全部替换为明确异常处理

### 测试
- 新增 SQLite 迁移测试（5 个）
- 新增日志清理测试（4 个）
- 测试总数从 12 增加到 ≥24

## [GitHub Actions 云端质量门禁] — 2026-07-15

### 新增

- `.github/workflows/ci.yml`：在 Windows 云端执行 SDK、3proxy 运行时、工作区、Debug/Release 构建、测试和覆盖率 Artifact 门禁。
- `.github/PULL_REQUEST_TEMPLATE.md`：增加 Full Check、warnings-as-errors、Nullable、`.guard/review-rules.md` 和文档同步自检项。
- `.github/ISSUE_TEMPLATE/bug_report.md`：规范 Bug 环境、复现步骤和脱敏日志。
- `.github/ISSUE_TEMPLATE/feature_request.md`：规范功能背景、验收标准、影响范围和环境上下文。

### 说明

- CI 使用临时父目录工作区清单兼容现有 `validate-workspace.ps1`，不把本机开发工作区文件或运行时产物提交到仓库。
- `main` 分支保护仍需仓库管理员在 GitHub 设置中将 `CI / quality-gate` 配置为 required status check。

## [GitHub 源码仓库整理] — 2026-07-15

### 新增

- `data/config.example.json`：不含真实凭据的配置模板。
- `scripts/prepare-runtime.ps1`：下载并校验固定版本 3proxy 0.9.7 x64 运行时。
- `LICENSE`：项目 MIT 许可证。
- `THIRD-PARTY-NOTICES.md`：3proxy 许可证、版本和 SHA-256 记录。

### 修改

- `.gitignore`：允许安全说明和脱敏模板进入仓库，继续排除本机数据、日志、构建产物和运行时文件。
- README、环境配置、运行时说明和数据目录说明：同步源码仓库的首次克隆与运行步骤。

### 发布边界

- 真实 `data/config.json`、运行日志、报告、生成 cfg 和 3proxy 二进制不上传。
- 完整构建、测试和推送审计完成后再确认远端 `main`。

## [XAML 编译和代码质量修复] — 2026-07-15

### 修复
- **WPF XAML 编译错误**
  - 问题：5 个 XAML 编译文件缺失（MainWindow.g.cs、App.g.cs、MainView.g.cs、AddProxyWindow.g.cs、GeneratedInternalTypeHelper.g.cs）
  - 原因：构建输出缓存不同步
  - 解决方案：执行 `dotnet clean` → `dotnet restore` → `dotnet build`
  - 结果：所有文件正确生成

- **代码警告**
  - 文件：ExceptionHandler.cs
  - 警告：CS8601 - Possible null reference assignment
  - 原因：TryCatch<T> 返回 default(T) 可能为 null，但返回类型为 non-nullable T
  - 修复：将返回类型改为 `T?`，参数改为 `T? defaultValue = default`

### 验证
- ✅ 构建结果：0 errors, 0 warnings
- ✅ 单元测试：7/7 tests passed

## [项目根目录清理和配置优化] — 2026-07-15

### 删除
- 文件 `YLproxy.slnx`：自动生成的 VS 2022 格式解决方案文件（不应提交 Git）
- 文件 `test_path.cs`：临时测试文件位置错误（路径、目录获取测试代码）

### 保留
- 文件 `YLproxy.sln`：经典格式解决方案文件（作为主要构建入口）
- 文件 `AppSettings.json`：全局运行配置（作为根目录唯一配置入口）

### 修改
- 文件 `.gitignore`：补充缺失的忽略规则
  - 新增 `*.bak`（备份文件）
  - 新增 `*.orig`（merge 冲突原始文件）
  - 新增 `*.rej`（merge 冲突拒绝文件）
  - 新增 `*.vsix`（VS 扩展包）
  - 新增 `.DS_Store`（macOS 系统文件）
  - 新增 `Thumbs.db`（Windows 系统文件）

### 说明
- 项目根目录结构规范化完成，提高版本控制的清晰度和可维护性
- 消除了自动生成文件和临时测试文件的污染
- 完善 .gitignore 覆盖所有需要忽略的文件类型

## [运维部署文档优化] — 2026-07-15

### 新增
- `docs/development-deployment-outline/06-运维部署/API部署规范.md`：完整的部署架构、系统要求、详细部署步骤、配置管理指南、API 端点预留、监控与日志、故障排查。
- `docs/development-deployment-outline/06-运维部署/用户使用手册.md`：完整的界面介绍、基础操作指南、代理管理方法、进阶功能说明、常见场景处理、故障处理方案、最佳实践建议。
- `docs/development-deployment-outline/06-运维部署/部署流程.md`：详细的部署前检查、5 个部署阶段步骤、验证清单、性能基准测试、回滚策略、灾难恢复、自动化部署脚本。

### 修改
- `docs/development-deployment-outline/README.md`：更新部署与运维文档导航、更新快速导航发布流程指向、更新文档版本日志。

### 说明
- 完成 Phase E（运维部署）文档建设，覆盖统一 API 部署规范和用户端功能使用逻辑。
- 提供完整的部署、使用、运维、回滚流程指导。

## [配置唯一性与 C# 配置归一] — 2026-07-15

### 修改
- 全局配置服务由 `ConfigService` 统一命名为 `AppSettingsService`。
- 全局配置模型由 `AppConfig` 统一命名为 `AppSettingsConfig`。
- 强制全局配置只能使用根目录 `AppSettings.json`。
- 强制代理数据只能使用 `data/config.json`。

### 删除
- 未使用的 `IConfigService` 接口。
- 未使用的 `UpdateSection` 配置写入入口。
- 旧的 `ConfigService.cs` 文件名。

### 验证
- 活动 JSON 配置仅保留 `AppSettings.json` 和 `data/config.json`。
- 配置测试 7 Passed, 0 Failed。
## [配置一致性治理] — 2026-07-15

### 修改
- `.agent`：统一目录树中的唯一规则文件名为 `.agent`，增加新建/移动文件的目录与调用方声明要求。
- 基础设施配置服务：增加全局配置值和规范目录校验。
- `LoggerFactory`：改为通过统一配置服务读取 `Logging`。
- `ProxyDataService`：统一代理数据服务名称，并限制唯一数据路径为 `data/config.json`。
- GUI、测试和模块文档同步使用 `ProxyDataService`。

### 新增
- `tests/ConfigurationContractTests.cs`：覆盖全局配置键、规范运行时目录和非法代理数据路径。

### 保留项
- 保留 `data/config.json` 中的本地代理数据。
- 保留现有 `runtime/3proxy/cfg` 文件，未执行运行时 cfg 清理。
## [剩余风险闭环：DPAPI 与真实 3proxy parent 链] — 2026-07-15

### 安全修复

- 将占位 Base64 改为 Windows DPAPI `CurrentUser` 加密。
- 新增 `ProxyDataSerializer`，同时保护用户名和密码，支持旧明文自动迁移。
- 配置文件采用临时文件加原子替换写入，降低中断导致的损坏风险。
- 当前 `data/config.json` 已迁移，旧明文 cfg 已删除。

### 代理链修复

- 将错误的 `-eHOST:PORT` 改为 3proxy 标准 `parent 1000 http HOST PORT [USER PASSWORD]`。
- 增加 `fakeresolve`，避免目标域名本地解析阻断 parent 链。
- 3proxy cfg 在启动失败、停止和异常退出路径清理。

### 测试

- 新增 DPAPI 往返、旧配置迁移和无明文序列化测试。
- 新增真实 3proxy 本地认证 parent 转发测试。
- 完整测试：10 Passed，0 Failed；构建：0 Error，0 Warning。

## [P0/P1/P2 工作区与验证链风险加固] — 2026-07-15

### 修复

- 修复 `scripts/full-check.ps1` 使用错误 GUI 输出路径和错误项目根目录的问题。
- 修复 Smoke Test 固定等待、异常退出不清理和真实配置可能被触碰的风险。
- 修复 Full Check 清理失败仍继续并报告成功的问题：现在自动重试，重试失败会终止检查。

### 新增

- `global.json`：固定 .NET SDK `10.0.301`，使用 `latestPatch`。
- `scripts/validate-workspace.ps1`：验证单机工作区 JSON、工程入口、SDK 和 3proxy 运行时。
- 父目录工作区任务：环境校验和显式隔离 Smoke Test。
- `.gitignore` 的 `reports/` 规则，避免验证报告混入项目工作区。

### 验证

- 环境校验通过。
- Full Check 通过：清理成功、构建 0 Error/0 Warning、测试 7 Passed。
- 隔离 GUI Smoke Test 通过，进程、临时数据和临时目录均已清理。

## [独立 VS Code 工作区配置] — 2026-07-15

### 新增

- 在父目录 `E:\GZQ\YLXCX` 新增 `YLproxy.code-workspace`，将 `YLproxy` 作为独立项目工作区。
- 增加统一编辑器编码、终端环境、搜索排除和文件监听排除设置。
- 增加 Restore、Build、Test、Run GUI、Full Check、Clean 任务。
- 增加 `YLproxy.GUI` 的 .NET `coreclr` 调试配置和开发扩展推荐。

### 验证

- 工作区 JSON 结构校验通过。
- `dotnet build YLproxy.sln --configuration Debug`：0 Error，0 Warning。
- `dotnet test tests/YLproxy.Tests.csproj --configuration Debug --no-build --no-restore`：7 Passed，0 Failed。

# 变更记录

## [Phase 2] — 2026-07-13

### 新增
- MVVM 基础框架：ViewModelBase、RelayCommand
- MainViewModel：本机信息、代理列表、日志、按钮命令
- ProxyRowViewModel：DataGrid 行数据模型
- MainView.xaml：三区域布局（本机信息 + 操作区 / DataGrid / 日志）
- docs/progress.md、docs/task-tracking.md、docs/deployment.md

### 修改
- MainWindow.xaml：嵌入 MainView UserControl
- App.xaml：启动 MainWindow
- MainView.xaml.cs：修复 using 引用

### 删除
- temp_wpf_test/ 残留测试项目

## [Phase 3B] — 2026-07-13

### 修改
- 修复 config.json 运行时路径：修正为仓库根目录下的 `data/config.json`（避免写入错误目录导致配置“丢失”）
- 实现删除功能：`RemoveCommand` 从选中项移除并持久化到配置文件
- 更新 UI 提示文本：与 Phase 3B（配置持久化）保持一致

## [Phase 4] — 2026-07-13

### 新增
- Views/AddProxyWindow.xaml：弹出式添加代理模态窗口 UI
- Views/AddProxyWindow.xaml.cs：窗口代码后置（取消/确定交互）
- ViewModels/AddProxyViewModel.cs：添加代理表单字段、校验、端口分配与保存逻辑
- Views/PasswordBoxHelper.cs：PasswordBox 附加属性辅助（用于密码同步）
- Views/InverseBoolConverter.cs：布尔值反转转换器（XAML 层使用）

### 修改
- MainViewModel.cs：AddCommand → ShowAddWindow() 弹出模态窗口；移除旧硬编码 AddProxyAndPersist()。

### 行为变化
- 添加代理时：名称非空、IP 格式、端口范围校验（1~65535）
- 本地端口自动分配：9001 起并支持手动输入；新增端口耗尽保护（9001~9100）
- 保存到 config.json：写入 LocalHost（本机 IPv4）与 LocalPort，新增后刷新列表。
- UI 提示文案更新为 Phase 4 状态描述。



## [Phase 5] — 2026-07-13


### 新增
- YLproxy.Core/ProxyTester.cs：代理可用性测试（HttpClient + WebProxy，Stopwatch 精确测量延迟）
  - 支持 HTTP 认证代理（username/password 同时存在才设置 Credentials）
  - 支持无认证代理（仅设置 WebProxy 地址）
  - 超时 10s（TaskCanceledException → “连接失败: 超时”）
  - 异常分类捕获（HttpRequestException → 连接失败详情）

### 修改
- YLproxy.GUI/MainViewModel.cs：TestCommand 异步调用 ProxyTester.TestAsync
  - 未选中代理：提示 “no proxy selected” 并写入日志
  - UI 非阻塞：BeginInvoke 追加日志

### 行为变化
- 点击“测试”：在运行日志中输出成功（延迟 ms）/失败（原因）。

## [Phase 6] — 2026-07-13

### 新增
- YLproxy.Proxy/ConfigGenerator.cs：根据 ProxyItem 生成 3proxy cfg 配置（service/log/auth/allow/internal/proxy/flush）
- YLproxy.Proxy/ProxyProcessManager.cs：管理 3proxy 进程生命周期（Start/Stop/IsRunning），写入 cfg 到 runtime/3proxy/cfg/{id}.cfg

### 修改
- YLproxy.Core.csproj：添加 YLproxy.Proxy 项目引用
- YLproxy.GUI/MainViewModel.cs：StartCommand/StopCommand 异步调用 ProxyProcessManager.Start/Stop
  - 状态更新：Running / Stopped / Failed
  - 修复 Status 不刷新 DataGrid：强制刷新 Proxies 集合
  - 删除代理前先停止对应 3proxy 进程，避免孤儿进程
- YLproxy.GUI/Views/MainView.xaml：操作区提示文案更新为 Phase 6

### 行为变化
- 点击"启动"：生成 cfg 文件 → 启动 3proxy.exe → DataGrid 状态显示 Running
- 点击"停止"：Kill 3proxy 进程 → 释放端口 → DataGrid 状态显示 Stopped
- 启动失败时状态自动回退 Failed

## [Phase 7] — 2026-07-13

### 新增
- YLproxy.Core/MonitorService.cs：后台状态监控服务
  - Timer 每 5 秒扫描所有 Running 状态的代理
  - 调用 ProxyProcessManager.IsRunning() 检测 3proxy 进程是否存活
  - 进程意外退出时 Status → Failed，写入日志
  - IDisposable 支持优雅清理

### 修改
- YLproxy.GUI/MainViewModel.cs：
  - 构造函数中初始化 MonitorService（注入 getProxies/logAction/refreshAction 回调）
  - 新增 RefreshDataGrid() 方法：强制刷新 DataGrid（解决 ProxyItem 无 INotifyPropertyChanged 问题）
  - 启动日志更新为 "Phase 7 with MonitorService"

### 行为变化
- 后台每 5 秒自动检测 3proxy 进程健康状态
- 进程被手动 kill 或崩溃后，5 秒内自动更新状态为 Failed

## [Phase 8] — 2026-07-15

### 修改
- 统一解决方案入口为 `YLproxy.sln`，修复错误的 `.slnx` 引用
- `src/YLproxy.Utils/PathResolver.cs`：仓库根目录识别标记由 `YLproxy.slnx` 调整为 `YLproxy.sln`
- `README.md`：目录说明与构建命令由 `YLproxy.slnx` 调整为 `YLproxy.sln`
- `docs/deployment.md`：构建命令修正为 `dotnet build YLproxy.sln`
- 通过 `dotnet sln YLproxy.sln migrate` 重建有效 `YLproxy.slnx`，恢复 `.slnx` 构建可用性

## [Phase 9] — 2026-07-15

### 新增
- `.editorconfig`：统一文本文件编码为 UTF-8，降低 Windows 环境误保存导致乱码的风险
- `.vscode/settings.json`：增加编码设置与 `DOTNET_CLI_UI_LANGUAGE=en-US` 终端环境变量

### 修改
- `.blackboxrules`：新增 dotnet 命令前置语言环境设置，避免日志乱码
- `README.md`：补充“终端乱码规避（Windows）”说明
- `.blackbox/tmp/shell_tool_0b6f357b97b3.log`：将已乱码文本修复为正确中文

### 原因与结果
- 根因：命令输出编码与读取端解码不一致（本地代码页 vs UTF-8）导致中文消息出现异常乱码
- 结果：已完成仓库内乱码修复与编码策略加固，构建/测试验证通过

## [UI 交互与状态锁修复] — 2026-07-15

### 修复
- `src/YLproxy.GUI/MainViewModel.cs`：
  - 修复 `TestSelectedProxyAsync`、`StartSelectedProxy`、`StopSelectedProxy` 状态锁（`IsTesting`, `IsStarting`, `IsStopping`）在执行后（不管是成功、失败还是抛出异常）未能在 `finally` 块中重置回 `false` 的问题。该问题曾导致点击一次对应按钮后，按钮变成永久禁用，且无法再次发起点击。
  - 在 `StartSelectedProxy` 与 `StopSelectedProxy` 异步线程由于 `Dispatcher.BeginInvoke` 恢复 UI 后，重置对应的控制锁，保证操作可靠回执。
- `src/YLproxy.GUI/App.xaml`：
  - 修复 `DataGrid` 选中项样式覆盖。由于 `DataGridCell` 将 `Background` 硬编码为 `Transparent`，导致点击 DataGrid 中对应的代理行时，选中高亮效果被其遮盖且没有任何视觉反馈（用户看起来好像点击无动作，无法选中）。
  - 新增 `DataGridRow` 样式并扩展 `DataGridCell` 的 `IsSelected` 状态样式；提供现代深色主题的 Hover（`#2D2D30`）以及 Active Selected（`#094771`）的点击渲染，极大改善了行选中和操作的点击反馈。

## [部署沉积清理与 TODO 重构] — 2026-07-15

### 新增

- 重构根目录 `TODO.md`，加入当前部署现状、风险、清理范围、P0-P5 执行方案和验收标准。
- 记录后续 P0 真实运行链验收、P1 DPAPI 安全加固、P2 日志与异常治理等阶段入口。

### 清理

- 删除构建缓存、临时路径验证输出、应用历史日志、3proxy 引擎历史日志和黑盒临时日志。
- 删除 GUI 下重复的运行日志目录，保留根日志说明文件。
- 保留用户代理配置、3proxy 二进制、模板和当前运行 cfg。

### 验证

- `dotnet build YLproxy.sln`：0 Error，0 Warning。
- `dotnet test tests/YLproxy.Tests.csproj`：7 Passed，0 Failed。
