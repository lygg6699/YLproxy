# TODO（Phase 4：弹出式添加窗口 + 校验/端口策略）

## Phase 3B（归档）
- [x] Step1 新建 Models：ProxyStatus.cs、ProxyItem.cs（包含 LocalHost 字段）、AppConfig.cs
- [x] Step2 重构 GUI：MainViewModel.cs 引用 ProxyItem 替代 ProxyRowViewModel，并移除 GUI 内 ProxyRowViewModel
- [x] Step3 重构 GUI：MainView.xaml DataGrid 列与 Binding 对齐（编号/上游IP/上游端口/账号/本地IP/本地端口/状态）
- [x] Step4 清理占位：删除 Models/Core/Proxy/Utils 的 Class1.cs
- [x] Step5 构建验证：dotnet build
- [x] Step6 文档同步：更新 docs/task-tracking.md、docs/progress.md、docs/changelog.md
- [x] Step7：手动验证 GUI：添加/删除是否正确写入 data/config.json 并刷新列表
- [x] Step8：端口分配起始值是否为 9001-9100（如需）
- [x] Step9（建议）：清理 MainWindow.xaml.cs 未使用 using

## Phase 4（进行中）
- [x] 新增：AddProxyWindow.xaml（500×450 模态弹窗）
- [x] 新增：AddProxyViewModel.cs（表单字段 + 校验 + 保存）
- [x] 修改：MainViewModel.cs AddCommand → ShowAddWindow() 弹出模态窗口，并移除旧硬编码方法
- [x] 行为：自动端口分配（9001 起）+ 重复检测
- [x] 修复：LocalHost 写入（本机 IPv4）避免本地IP列为空
- [x] 修复：端口分配无上限问题（增加 9001~9100 耗尽保护）
- [x] 修改：UI 提示文字切换为 Phase 4
- [x] 文档：更新 docs/changelog.md、docs/progress.md、docs/TODO.md 记录 Phase 4

## Phase 5（完成：代理测试集成）

- [x] 5.1 新增 YLproxy.Core/ProxyTester.cs（HttpClient + WebProxy 可用性测试、Stopwatch 延迟测量、10s 超时、异常分类）
- [x] 5.2 支持 HTTP 认证代理与无认证代理（Credentials / 仅 WebProxy 地址）
- [x] 5.3 修改 MainViewModel.TestCommand：异步调用 ProxyTester.TestAsync，写入运行日志并显示成功/失败原因
- [x] 5.4 未选中代理：输出 “no proxy selected” 到日志
- [x] 5.5 UI/日志：成功输出延迟（ms），失败输出原因（如超时）

## Phase 6（完成：3proxy 集成与启动/停止）
- [x] 6.0 Core.csproj 添加 Proxy 引用（YLproxy.Proxy）
- [x] 6.1/6.2：ConfigGenerator + ProxyProcessManager（生成 cfg + 管理 3proxy 进程）
- [x] 6.3/6.4：cfg 写入 runtime/3proxy/cfg/{id}.cfg + 启停逻辑/异常处理
- [x] 6.5/6.6：GUI 启动/停止命令接入 MainViewModel.cs
- [x] 6.7：状态更新 Running/Stopped/Failed + 异常回退 Failed
- [x] 6.8：修复 Status 更新不会刷新 DataGrid 的问题（强制刷新 Proxies 集合）
- [x] 6.9：删除代理前先停止进程，避免孤儿 3proxy
- [x] 6.10：更新 UI 提示文案为 Phase 6
- [x] 6.11：dotnet build + 手工测试（启动/停止 + cfg 生成）

