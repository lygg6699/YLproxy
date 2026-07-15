# YLproxy.GUI/

## 目录简介

WPF 图形界面层，使用 MVVM (Model-View-ViewModel) 模式构建的用户界面。提供直观的代理管理体验，让用户无需了解 3proxy 即可使用代理转换功能。

## 主要文件/子目录说明

### Views/
XAML 界面文件：
- `MainView.xaml`：主界面，包含本机信息、操作区、代理列表和运行日志
- `AddProxyWindow.xaml`：添加代理弹窗，包含表单验证和输入控件

### ViewModels/
MVVM 逻辑层：
- `MainViewModel.cs`：主视图模型，协调所有业务操作和状态更新
- `AddProxyViewModel.cs`：添加代理视图模型，处理表单验证和配置保存

### MainWindow.xaml
主应用程序窗口：
- 设置窗口标题、尺寸和基本属性
- 将 MainView 设置为主要内容
- 将 DataContext 设置为 MainViewModel 实例

### 支持文件
- `ViewModelBase.cs`：MVVM 基类，实现 INotifyPropertyChanged 和 SetProperty 方法
- `RelayCommand.cs`：命令实现，将 UI 事件绑定到 ViewModel 方法
- `InverseBoolConverter.cs`：布尔值转换器，用于按钮禁用状态的反转绑定

## 使用说明

用户通过 GUI 操作代理：
1. 启动应用后看到主界面，显示本机信息（电脑名、IP、网络状态、时间）
2. 在「代理管理」选项卡中：
   - 点击「添加代理」打开添加窗口
   - 填写代理信息（名称、服务器IP、端口、用户名、密码）
   - 点击「测试连接」验证代理可用性
   - 点击「确定」保存代理并返回主界面
   - 在代理列表中看到新添加的代理（初始状态为停止）
   - 选择代理后点击「启动」开始代理服务
   - 点击「停止」停止代理服务
   - 点击「删除」删除代理配置（会先停止再删除）
3. 实时功能：
   - 底部日志框显示所有操作和系统事件
   - 右上角定时更新显示当前时间
   - 网络状态和 IP 地址在后台每秒更新一次
   - 后台按 `AppSettings.json` 的 `Proxy.CheckIntervalSeconds` 检查代理进程状态（默认 5 秒）

## 注意事项

1. **按钮防重复点击**：
   - 已部分实现 IsTesting、IsStarting、IsStopping 标志
   - 建议完善所有操作按钮的防重复点击机制
   - 考虑使用命令的 CanExecute 机制替代手动标志

2. **UI 刷新机制**：
   - ProxyItem 目前不实现 INotifyPropertyChanged
   - 状态更新通过强制重新添加集合项来触发 UI 刷新
   - 建议考虑让 ProxyItem 实现 INotifyPropertyChanged 以获得更自然的绑定

3. **错误处理和用户反馈**：
   - 所有操作通过日志框提供反馈
   - 建议考虑添加 toast 通知或模式对话框 для 重要操作
   - 当前验证错误通过 ValidationMessage 属性显示在添加窗口底部

## 后续计划

- 增加暗色主题支持（切换浅色/深色主题）
- 添加系统托盘支持（最小化到托盘，双击恢复）
- 实现启动全部/停止全部按钮
- 添加批量导入功能（支持 CSV 或 JSON 格式）
- 开机启动选项和后台服务模式
- 代理使用统计和流量监控
- 更详细的代理信息显示（延迟、请求数、流量等）