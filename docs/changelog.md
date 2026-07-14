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




