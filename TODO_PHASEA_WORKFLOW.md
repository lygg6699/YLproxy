# TODO_PHASEA_WORKFLOW.md

## 0. 当前状态（基于已核验事实）
- Build：`dotnet build YLproxy.sln` 已通过（存在警告，不阻断）。
- 单测门禁：`dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E` 已通过。
- Phase A 验收口径：A1~A4 尚未全部落地（MainViewModel 未拆分；接口抽取/DI 链闭合尚未完成；XAML 子属性绑定未改造；ProxyItem CreateTime init-only 未实现）。

## 1. Phase A 执行计划（按可编译/可测试顺序推进）

### Step A1. MainWindow / ViewModel 创建链路走 DI（启动链闭合）
- 修改：`src/YLproxy.GUI/App.xaml.cs`
  - DI 注册：补齐 `MainViewModel` 所需的依赖（至少需要：ILogger、AppSettingsService、ProxyConfig、ThreeProxyConfig）。
- 修改：`src/YLproxy.GUI/MainWindow.xaml.cs`
  - 目标：移除对 `new MainViewModel()` 或任何非 DI 的 new 依赖链。
  - 当前：仅通过 `DataContext is MainViewModel vm` 做按键命令触发，无需修改业务逻辑。
- 修改：`src/YLproxy.GUI/MainViewModel.cs`
  - 将 `MainViewModel()` 无参构造迁移为带依赖注入构造（保留可运行入口，确保 build 通过）。
  - 将 `new GlobalConfigService()`、`LoggerFactory.CreateLogger()`、`new ProxyDataService()` 等“内部 new 依赖链”最小化，优先做到 MainViewModel 初始化部分走 DI。

**通过标准（已复验证据）：**
- `dotnet build YLproxy.sln`：Build succeeded（warning 不阻断）。
- `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

### Step A2. 接口抽取（按目录结构与命名目标落地）
- 新增（由接口定义驱动）：
  - `src/YLproxy.Core/Abstractions/IProxyDataService.cs`
  - `src/YLproxy.Core/Abstractions/IProxyTester.cs`
  - `src/YLproxy.Core/Abstractions/IProxyRepository.cs`
  - `src/YLproxy.Proxy/Abstractions/IProxyProcessManager.cs`
  - `src/YLproxy.Infrastructure/Abstractions/IAppSettingsService.cs`
- 修改实现类以实现接口，并在 DI 注册中替换为接口依赖。
- 修改 MainViewModel / 子 VM（后续 A3）依赖接口而非具体类。

**通过标准：**
- build/test 仍全绿

### Step A3. God Class 拆分（HostInfo / Dashboard / LogPanel）
- 新增子 VM：
  - `HostInfoViewModel`
  - `DashboardViewModel`
  - `LogPanelViewModel`
  - `ProxyOperationViewModel`（或等价结构）
- 修改：`src/YLproxy.GUI/Views/MainView.xaml`
  - 将绑定从 `{Binding ComputerName}` 变为 `{Binding HostInfo.ComputerName}` 等子属性路径。
- 修改：`src/YLproxy.GUI/MainViewModel.cs`
  - MainViewModel 作为协调器，组合并暴露：`HostInfo` / `Dashboard` / `LogPanel` / `Proxies` 等。

**通过标准：**
- GUI 编译通过
- 单测门禁通过

### Step A4. ProxyItem 模型改造（CreateTime init-only 约束）
- 修改：`src/YLproxy.Models/ProxyItem.cs`
  - CreateTime 改为 `public DateTime CreateTime { get; init; } = DateTime.UtcNow;`
  - 同时确保反序列化/手动创建代码能够编译。
- 修改所有创建/反序列化的代码点。

**通过标准：**
- build/test 通过

### Step A5. 文档同步（必须与代码证据一致）
- 修改：
  - `TODO.md`
  - `docs/progress.md`
  - `docs/task-tracking.md`
  - `docs/changelog.md`
- 必须在文档中体现：
  - Phase A 的每个交付物（A1~A4）对应的文件路径
  - 对应验证命令与结果（build/test）

## 2. 迭代节奏
- 每完成 Step，先 `dotnet build` + `dotnet test --filter TestCategory!=E2E` 复验。
- 再更新文档证据段落。

