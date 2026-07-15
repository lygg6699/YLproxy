# YLproxy.Models/

## 目录简介

存放数据模型，定义项目的核心数据结构。所有业务逻辑和 GUI 都依赖这些模型来表示代理配置和状态。
代理配置对象，包含以下属性：
- `Id`：唯一标识符
- `Name`：代理名称（用户自定义）
- `RemoteHost`：上游代理服务器 IP 地址
 ProxyDataService 的 JSON 序列化/反序列化逻辑
- `LocalHost`：本地绑定 IP 地址（通常为 127.0.0.1）
- `LocalPort`：本地代理端口（自动分配 9001-9100）
- `Status`：代理当前状态（ProxyStatus 枚举）
- `CreateTime`：创建时间戳

### ProxyStatus.cs
代理状态枚举，定义三种状态：
- `Stopped = 0`：已停止
- `Running = 1`：运行中
- `Failed = 2`：运行失败

### AppConfig.cs
配置容器类，包含：
- `Proxies`：ProxyItem 对象的列表，用于 JSON 序列化/反序列化

## 使用说明

所有业务逻辑和 GUI 层都依赖这些模型：
- GUI 通过数据绑定显示 ProxyItem 属性
- ProxyDataService 将 AppConfig 序列化/反序列化到 data/config.json
- ProxyProcessManager 使用 ProxyItem 生成 3proxy 配置并管理进程
- MonitorService 检查 ProxyItem.Status 来判断是否需要监控

## 注意事项

1. **模型修改同步**：如果修改了 ProxyItem 或 AppConfig 的属性，需要同步更新：
   - ConfigService 的 JSON 序列化/反序列化逻辑
   - GUI 的数据绑定和验证
   - 数据迁移脚本（未来迁移到 SQLite 时）

2. **属性初始化**：所有字符串属性使用 `{ get; init; } = string.Empty;` 初始化，避免 null 引用

3. **状态一致性**：ProxyStatus 枚举值必须与数据库和 UI 显示保持一致

## 后续计划（可选）

- 考虑添加更多状态值（如 Starting、Stopping）以改善状态转换的细粒度控制
- 添加数据验证特性（Data Annotations）以改进模型验证
- 考虑实现 INotifyPropertyChanged 接口以支持更好的数据绑定（虽然当前通过手动刷新实现）
