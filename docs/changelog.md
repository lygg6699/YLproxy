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
