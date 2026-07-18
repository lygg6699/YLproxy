# YLproxy 开发进度追踪

> 本文件记录 YLproxy 项目的开发进度、Phase 完成状态、构建测试结果和里程碑追踪

**最后更新：** 2026-07-19  
**文档状态：** ✅ 文档结构已重组，待完善部分已更新

---

## Phase A3：子 ViewModel 组合模式（2026-07-19）

**状态：已完成（build/test 复验通过）**

- ✅ 新增 `HostInfoViewModel`、`DashboardViewModel`、`LogPanelViewModel` 三个子 ViewModel。
- ✅ `MainViewModel` 引入子 ViewModel 属性作为协调器，移除内联的 HostInfo/Dashboard/Log 重复属性。
- ✅ `MainView.xaml` 绑定路径从 `{Binding ComputerName}` 改为 `{Binding HostInfo.ComputerName}` 等。
- ✅ `MainView.xaml.cs` 的 `CollectionChanged` 订阅从 `_subscribedVm.FilteredLogs` 改为 `_subscribedVm.LogPanel.FilteredLogs`。
- ✅ `ClearLogCommand` 委托到 `LogPanel.ClearLogCommand`。
- ✅ 验证复验：
  - `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
  - `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## Phase A4：ProxyItem.CreateTime init-only 化（2026-07-19）

**状态：已完成（build/test 复验通过）**

- ✅ `src/YLproxy.Models/ProxyItem.cs`：`CreateTime { get; set; }` → `CreateTime { get; init; }`。
- ✅ 全代码仓扫描确认无 `obj.CreateTime = value` 赋值语法（仅对象初始化器使用），无破坏点。
- ✅ 验证复验：
  - `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
  - `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## Phase A1：DI 注册 + MainViewModel 构造链闭合（2026-07-18）

**状态：已完成（build/test 复验通过）**


- ✅ 修改 `src/YLproxy.GUI/App.xaml.cs`：补齐 DI 注册并通过 DI 创建 `MainViewModel`（使用 `ServiceLocator` 初始化 provider）。
- ✅ 修改 `src/YLproxy.GUI/MainViewModel.cs`：`MainViewModel()` 无参构造迁移为带依赖注入构造（ILogger / AppSettingsService / ProxyConfig / ThreeProxyConfig）。
- ✅ 验证复验：
  - `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
  - `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## Phase A2：接口抽取（AppSettings/IProxy... 接口契约对齐）（2026-07-18）

**状态：已完成（关键编译闭环）**

- ✅ `IAppSettingsService.GetConfig()` 与 `AppSettingsConfig` 类型对齐（修复返回类型不一致导致的转换失败/编译阻断）。
- ✅ `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
- ✅ `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## Phase 2.5 代理认证与网络连接修复（2026-07-15）


**状态：✅ 已完成**

- ✅ 修复“带账号密码代理无法连接网络”的问题：当传入请求完全不带任何认证头时，Forwarder 也能正确注入真实的 Proxy-Authorization 凭据。
- ✅ 增强 `TransparentCoalescingForwarder` 的认证头注入逻辑，确保在无认证头、已有认证头等各种场景下均能正确、安全地注入上游代理凭据。
- ✅ 新增单元测试 `ForwarderShouldInjectAuthHeaderEvenIfIncomingRequestHasNoAuth` 验证该修复方案的正确性。

## Job Object 孤儿进程防护与 CI 加固（2026-07-15）

**状态：✅ 已完成**

- ✅ 编辑 `.github/workflows/ci.yml`，在 `dotnet test` 步骤中通过 `--filter` 隔离依赖物理网络/DPAPI 的集成测试。
- ✅ 确保 CI 流程中包含 `dotnet build` Debug/Release 且开启警告转错误（`-warnaserror`）门禁。
- ✅ 规划并追踪 Job Object 孤儿进程防护机制。

## GitHub Actions 云端质量门禁（2026-07-15）

**状态：仓库配置已完成，远端 main 分支保护待 GitHub 管理权限执行**

### 已完成

- ✅ 新增 `.github/workflows/ci.yml`，使用 `windows-latest` 执行 checkout、global.json SDK 准备、3proxy 运行时准备、工作区校验、Debug/Release warnings-as-errors 构建、测试和覆盖率 Artifact 上传。
- ✅ 新增 PR 合并质量自检模板。
- ✅ 新增 Bug 报告和功能申请 Issue 模板，要求填写 .NET、3proxy、复现步骤和脱敏日志。
- ✅ CI 通过临时父目录工作区清单兼容 `scripts/validate-workspace.ps1` 对本机开发工作区的校验要求。

### 待完成

- ⏳ 在仓库管理员权限下，将 `CI / quality-gate` 设置为 `main` 的 required status check，并启用仅允许 PR 合并、分支保持最新、禁止强推和删除分支。

## 剩余风险闭环：DPAPI 与真实 3proxy parent 链（2026-07-15）

**状态：核心风险已完成，外部供应商验收待现场执行**

- ✅ 使用 Windows DPAPI `CurrentUser` 保护代理用户名和密码，存储格式带 `dpapi:v1:` 前缀。
- ✅ `ProxyDataSerializer` 加载时解密旧格式并自动迁移，保存时始终加密，配置写入采用原子替换。
- ✅ 当前 `data/config.json` 的 2 条代理记录已完成真实迁移，用户名和密码均不再明文落盘。
- ✅ 3proxy 配置由错误的 `-e远程地址` 改为 `parent 1000 http HOST PORT [USER PASSWORD]`，并启用 `fakeresolve`。
- ✅ 运行 cfg 在启动失败、正常停止和监控发现进程退出时清理，降低运行时明文残留风险。
- ✅ 新增 DPAPI 迁移测试和真实 3proxy parent 转发测试，完整测试通过 10/10。
- ✅ 删除旧的 `1.cfg`、`2.cfg`、`999.cfg` 明文运行配置；保留无凭据测试配置和 3proxy 样例。

### 尚未闭环

- ⏳ 真实外部上游代理供应商的凭据、目标网站、单代理/多代理和异常退出现场验收仍需在目标网络环境执行。
- ⏳ DPAPI 使用当前 Windows 用户作用域，跨用户或跨机器迁移必须通过重新录入凭据，不支持直接复制密文恢复。

## P0/P1/P2 工作区与验证链风险加固（2026-07-15）

**状态：已完成**

- ✅ 修复 `scripts/full-check.ps1` 的项目根目录定位和过期 GUI 输出路径。
- ✅ Smoke Test 使用系统临时目录中的隔离项目副本和空代理配置，不读取或覆盖真实 `data/config.json`。
- ✅ 增加 10 秒启动轮询、异常退出检测、进程终止和临时目录清理；清理旧编译失败时自动重试一次。
- ✅ 保留父目录 `YLproxy.code-workspace` 作为单机启动入口，新增 `scripts/validate-workspace.ps1` 校验其 JSON、任务、调试配置和关键路径。
- ✅ 新增 `global.json` 固定 .NET SDK `10.0.301`，使用 `latestPatch` 策略。
- ✅ 增加环境校验任务、显式 Smoke Test 任务，并将 `reports/` 加入 Git 忽略。
- ✅ 完整验证通过：清理成功、构建 0 Error/0 Warning、测试 7 Passed、隔离 Smoke Test 启动和清理成功。

## 独立 VS Code 工作区配置（2026-07-15）

**状态：已完成**

- ✅ 在父目录 `E:\GZQ\YLXCX` 创建 `YLproxy.code-workspace`，将 `YLproxy/` 作为唯一工作区文件夹。
- ✅ 集中配置 UTF-8 编码、Windows .NET CLI 输出语言、搜索/文件监听排除规则和扩展推荐。
- ✅ 添加 Restore、Debug/Release 构建、测试、GUI 运行、Full Check 和 Clean 任务。
- ✅ 添加 WPF GUI 的 `coreclr` 调试配置，并以 `YLproxy.sln` 作为解决方案入口。
- ✅ 工作区 JSON 结构校验通过。
- ✅ `dotnet build YLproxy.sln --configuration Debug`：成功，0 Error，0 Warning。
- ✅ `dotnet test tests/YLproxy.Tests.csproj --configuration Debug --no-build --no-restore`：7 Passed，0 Failed。

## XAML 编译和代码质量修复（2026-07-15）

**状态：已完成**

- ✅ 解决 WPF XAML 编译问题：执行 `dotnet clean` → `dotnet restore` → `dotnet build`，重新生成 MainWindow.g.cs、App.g.cs 等 5 个缺失的 XAML 编译文件
- ✅ 修复 null reference 警告：ExceptionHandler.cs TryCatch<T> 方法返回类型改为 `T?` 以正确处理可能为 null 的默认值
- ✅ 构建零警告（0 warnings）
- ✅ 单元测试全部通过（7/7 tests passed）

## 项目根目录清理和配置优化（2026-07-15）

**状态：已完成**

- ✅ 删除 `YLproxy.slnx`（自动生成的 VS 2022 格式文件，不应提交 Git）
- ✅ 删除 `test_path.cs`（临时测试文件位置错误，已清理）
- ✅ 确认 `YLproxy.sln` 保留（作为主要解决方案文件）
- ✅ 确认 `AppSettings.json` 保留（全局运行配置，唯一配置入口）
- ✅ 更新 `.gitignore` 补充缺失规则（*.bak, *.orig, *.rej, *.vsix, .DS_Store, Thumbs.db）
- ✅ 项目根目录结构规范化完成

## 运维部署文档优化（2026-07-15）

**状态：已完成**

- ✅ 创建 `docs/development-deployment-outline/06-运维部署/API部署规范.md`（部署架构、配置管理、API 端点）
- ✅ 创建 `docs/development-deployment-outline/06-运维部署/用户使用手册.md`（界面介绍、基础操作、进阶功能）
- ✅ 创建 `docs/development-deployment-outline/06-运维部署/部署流程.md`（详细部署步骤、验证清单、回滚方案）
- ✅ 更新 `docs/development-deployment-outline/README.md` 导航索引和文档版本日志
- ✅ 完成 Phase E（运维部署）文档建设

## 配置唯一性与 C# 配置归一（2026-07-15）

- ✅ 全局配置服务统一为 `AppSettingsService`，根配置模型统一为 `AppSettingsConfig`。
- ✅ 代理数据服务唯一保留为 `ProxyDataService`，唯一数据文件为 `data/config.json`。
- ✅ 删除未使用的 `IConfigService` 和 `UpdateSection` 写入入口。
- ✅ 配置 JSON 读写入口、活动 JSON 文件和路径约束完成扫描。
- ✅ 验证测试：7 Passed, 0 Failed。
## 配置一致性治理

**状态：已完成（代码与自动化验证）**

- 修正 `.agent` 目录树中的唯一规则文件名，并强制新文件声明目标目录与调用方。
- 全局 `AppSettings.json` 增加日志、代理数据、端口、监控间隔和 3proxy 路径约束。
- `LoggerFactory` 统一通过基础设施配置服务读取 `Logging`。
- 代理数据服务统一命名为 `ProxyDataService`，并强制使用根目录 `data/config.json`。
- 新增配置契约和规范运行时目录测试，6 Passed, 0 Failed。
- 保留 `data/config.json` 本地数据及现有 `runtime/3proxy/cfg` 文件，未执行运行时配置清理。
# 开发进度记录

## GitHub 源码仓库整理与发布准备

**状态：已完成本地整理，待远端推送确认**

**完成时间：** 2026-07-15

### 完成内容

- ✅ 明确源码仓库边界：排除真实代理数据、日志、报告、构建产物、生成 cfg 和 3proxy 二进制
- ✅ 新增 `data/config.example.json` 脱敏模板，保留 `data/README.md` 作为数据目录说明
- ✅ 新增固定版本 3proxy 0.9.7 x64 运行时准备脚本和 SHA-256 校验说明
- ✅ 补充 MIT 许可证和第三方依赖声明
- ✅ 同步 README、环境配置和运行时目录说明
- ⏳ 完成本地完整验证、提交并推送到 `https://github.com/lygg6699/YLproxy.git`

### 发布边界

- `data/config.json` 只保留在本机，禁止通过 `git add -f` 加入提交
- `runtime/3proxy/bin64/` 由 `scripts/prepare-runtime.ps1` 生成，不进入 Git 历史
- `runtime/3proxy/cfg/`、`runtime/3proxy/logs/`、根 `logs/` 和 `reports/` 均为本机运行产物

## Phase 2 — MVVM 静态 GUI 基础结构

**状态：已完成**

**完成时间：** 2026-07-13 12:09

### 完成内容

- ✅ MVVM 基础框架（ViewModelBase + RelayCommand）
- ✅ MainWindow + MainView 布局
- ✅ MainViewModel（本机信息、代理列表、日志、按钮命令）
- ✅ ProxyRowViewModel（DataGrid 行数据模型）
- ✅ 本机信息区域（电脑名、IP、网络状态、时间）
- ✅ 操作区（添加/删除/测试/启动/停止 按钮）
- ✅ 代理列表 DataGrid（Host/Port/Type/Status）
- ✅ 运行日志 ListBox
- ✅ 当前时间实时刷新（Timer 每秒）
- ✅ 网络状态实时刷新
- ✅ 构建通过（0 Error, 0 Warning）

### Phase 3B（配置持久化）

- ✅ 新增 ProxyDataService：config.json 加载与保存
- ✅ 启动时从配置读取 Proxies 替换假数据
- ✅ 新增代理时写入 LocalHost/分配 LocalPort 并保存

### Phase 4（弹出式添加窗口 + 校验/端口策略）

- ✅ 创建 AddProxyWindow 模态弹窗并接入 MainViewModel AddCommand
- ✅ AddProxyViewModel：表单校验（名称/IP/端口范围）与保存到 config.json
- ✅ 本地端口自动分配（9001 起）与重复检测
- ✅ 新增端口耗尽保护（9001~9100）
- ✅ 写入 LocalHost（本机 IPv4），DataGrid 列展示完整
- ✅ 更新 UI 提示文案为 Phase 4

## Phase 5（代理测试集成）

- ✅ 新增 YLproxy.Core/ProxyTester.cs：测试代理可用性并精确测量延迟
- ✅ 支持 HTTP 认证代理（Credentials）与无认证代理（仅 WebProxy 地址）
- ✅ 超时（10s）与异常分类输出到运行日志
- ✅ MainViewModel TestCommand 异步调用 ProxyTester.TestAsync，UI 非阻塞
- ✅ 未选中代理时输出 "no proxy selected" 到日志

## Phase 6（3proxy 集成与启动/停止）

**状态：已完成**

- ✅ YLproxy.Core 引用 YLproxy.Proxy（3proxy 集成模块）
- ✅ YLproxy.Proxy/ConfigGenerator.cs：生成 3proxy cfg（service/log/auth/allow/internal/proxy/flush）
- ✅ YLproxy.Proxy/ProxyProcessManager.cs：写入 cfg 到 runtime/3proxy/cfg/{id}.cfg，并启动/停止 3proxy.exe
- ✅ MainViewModel.StartCommand/StopCommand：调用 ProxyProcessManager.Start/Stop，并更新 Running/Stopped/Failed 状态
- ✅ 修复 Status 更新无法刷新 DataGrid 的问题（强制刷新 Proxies 集合）
- ✅ 删除代理时先停止对应 3proxy 进程，避免孤儿进程
- ✅ 更新 MainView.xaml 操作区提示文案为 Phase 6

## Phase 7（后台状态监控）

**状态：已完成**

- ✅ 创建 YLproxy.Core/MonitorService.cs（Timer 每 5 秒检测所有 Running 代理的 3proxy 进程）
- ✅ 调用 ProxyProcessManager.IsRunning() 检测进程存活
- ✅ 进程意外退出时 Status → Failed，自动写入日志
- ✅ MainViewModel 构造函数中初始化 MonitorService（注入回调）
- ✅ 新增 RefreshDataGrid() 强制刷新 DataGrid（解决 ProxyItem 无 INotifyPropertyChanged 问题）
- ✅ 添加 ProxyProcessManager.IsRunning() 方法
- ✅ dotnet build（0 Error, 0 Warning）

## Phase 8（解决方案文件修复）

**状态：已完成**

- ✅ 修复无效 `YLproxy.slnx` 引发的构建入口混淆问题，统一使用 `YLproxy.sln`
- ✅ 更新 `PathResolver` 根目录识别标记：`YLproxy.slnx` → `YLproxy.sln`
- ✅ 同步 README / deployment 文档中的构建命令与目录说明
- ✅ `dotnet build YLproxy.sln`（0 Error, 0 Warning）
- ✅ `dotnet test tests/YLproxy.Tests.csproj`（2 Passed, 0 Failed）
- ✅ 通过 `dotnet sln YLproxy.sln migrate` 重建有效 `YLproxy.slnx`，`dotnet build YLproxy.slnx` 验证通过

## Phase 9（终端乱码修复与编码统一）

**状态：已完成**

- ✅ 定位根因：Windows 终端/日志在本地代码页与 UTF-8 解码不一致，导致中文输出出现异常乱码
- ✅ 新增 `.editorconfig`，统一文本文件 UTF-8 编码策略
- ✅ 新增 `.vscode/settings.json` 编码与终端环境约束（`files.encoding=utf8`、`DOTNET_CLI_UI_LANGUAGE=en-US`）
- ✅ 更新 `.blackboxrules`：固定 dotnet 命令前置语言环境设置
- ✅ 修复已乱码文件：`.blackbox/tmp/shell_tool_0b6f357b97b3.log`
- ✅ 验证构建：`dotnet build YLproxy.sln`（0 Error, 0 Warning）
- ✅ 验证测试：`dotnet test tests/YLproxy.Tests.csproj`（2 Passed, 0 Failed）

## UI 交互与状态锁修复

**状态：已完成**

- ✅ 修复 `IsTesting`、`IsStarting`、`IsStopping` 状态锁未能重置回 `false` 进而导致按钮第二次点击无反应且永久禁用的问题
- ✅ 修复 `DataGrid` 选中背景透明导致用户点击代理行没有任何高亮选中视觉反馈的问题
- ✅ 优化 DataGrid 选择和 Hover 的点击反馈，采用 VS 暗色主题风格（Hover: `#2D2D30`, Active Selected: `#094771`）
- ✅ `dotnet build YLproxy.sln`（0 Error, 0 Warning）已验证成功
- ✅ `dotnet test tests/YLproxy.Tests.csproj`（2 Passed, 0 Failed）已验证成功

## 部署沉积清理与 TODO 重构（2026-07-15）

**状态：已完成**

- ✅ 重构根目录 `TODO.md`，补充当前已落实部署内容、未闭环风险、清理边界、P0-P5 后续方案和验收标准。
- ✅ 清理根目录及各项目 `bin/`、`obj/`、`path_verif` 构建输出和 MSBuild/NuGet 缓存。
- ✅ 清理根 `logs/` 历史应用日志、`runtime/3proxy/logs/` 引擎日志和 `.blackbox/tmp/` 临时日志。
- ✅ 删除 `src/YLproxy.GUI/logs/` 重复运行日志目录，保留根 `logs/README.md`。
- ✅ 保留 `AppSettings.json`、`data/config.json`、`runtime/3proxy/bin64/` 和 `runtime/3proxy/cfg/`，未触碰用户数据、第三方运行时和代理运行配置。
- ✅ 清理后的工作区执行 `dotnet build YLproxy.sln`：成功，0 Error，0 Warning。
- ✅ 执行 `dotnet test tests/YLproxy.Tests.csproj`：7 Passed，0 Failed。

### 下一步

- ⏳ P0：完成真实 3proxy 单代理/多代理、端口监听、异常退出和回滚验收。
- ⏳ P1：使用 Windows DPAPI 加密代理凭据并完成旧配置迁移。

## Phase B：技术债偿还（2026-07-19）

**状态：B1/B2/B5 核心修复已完成（build/test 复验通过）**

### B1 — 阻塞性 P0 Bug 修复
- ✅ `RefreshDataGrid()` 空方法修复：实现 DataGrid 刷新逻辑，触发 FilteredProxies 集合重置和 UI 刷新
- ✅ async-over-sync 死锁风险修复：`ProxyDataService.LoadFromJson()` 和 `Save()` 改用同步 `_ioLock.Wait()`

### B2 — 接口对齐 + DI 闭环
- ✅ `ProxyProcessManagerAdapter` 创建：实现 `IProxyProcessManager` 接口，适配静态 `ProxyProcessManager`
- ✅ `ProxyTesterAdapter` 创建：实现 `IProxyTester` 接口，适配静态 `ProxyTester`
- ✅ `ProxyDataService` 实现 `IProxyDataService` 接口
- ✅ `SqliteProxyRepository` 实现 `IProxyRepository` 接口
- ✅ `MainViewModel` 全部静态调用替换为注入接口（`_proxyProcessManager`、`_proxyDataService`、`_proxyTester`）
- ✅ DI 注册补齐：`App.xaml.cs` 注册 3 个适配器 + 1 个数据服务
- ✅ 移除 `Proxy.Abstractions.ThreeProxyConfig` 空占位符，消除命名冲突
- ✅ 修复 `IProxyProcessManager` 接口中 `ThreeProxyConfig` 引用歧义

### B5 — 安全与正确性加固
- ✅ `TransparentCoalescingForwarder` 空 catch 改为结构化日志记录
- ✅ `AesSecurityService` 移除未使用的 `_keyLock` 死代码

### 验证
- ✅ `dotnet build YLproxy.sln`：0 Error, 36 Warnings（从 52 降至 36）
- ✅ `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：75/75 Passed
