# 开发进度记录

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

- ✅ 新增 ConfigService：config.json 加载与保存
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