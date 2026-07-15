# YLproxy.Core/

## 目录简介

核心业务逻辑层，负责配置管理、后台监控和代理测试等核心功能。此层作为 GUI 和 3proxy 集成层之间的桥梁，提供统一的业务接口。

## 主要文件/子目录说明

### Config/ProxyDataService.cs
JSON 配置读写服务：
- 负责读取和写入 `data/config.json` 文件
- 提供同步和异步的 Load/Save 方法
- 自动创建不存在的目录和文件
- 包含错误处理机制，避免配置问题导致应用崩溃

### MonitorService.cs
按全局配置定时监控代理进程：
- 使用 System.Threading.Timer 按 `AppSettings.json` 的 `Proxy.CheckIntervalSeconds` 执行检查（默认 5 秒）
- 仅监控状态为 Running 的代理
- 调用 ProxyProcessManager.IsRunning() 检查进程状态
- 当检测到进程异常退出时，将状态更新为 Failed
- 通过日志和 UI 刷新回调通知状态变化
- 包含异常处理防止定时器崩溃

### ProxyTester.cs
测试上游代理连通性：
- 使用 HttpClient 和 WebProxy 进行代理连接测试
- 支持有认证和无认证两种代理类型
- 测试目标为 http://www.baidu.com（可修改）
- 返回成功状态、延迟毫秒数和错误信息
- 包含超时处理（10秒）和异常捕获
- 防止重复点击的机制（在 GUI 层通过 IsTesting 标志）

## 使用说明

GUI 与后端交互的主要逻辑依赖此层：
- MainViewModel 通过 ProxyDataService 加载和保存代理配置
- MainViewModel 使用 ProxyTester 异步测试代理连通性
- MainViewModel 通过 MonitorService 实现后台状态监控
- MainViewModel 创建并协调这些服务，配置路径由 `PathResolver` 统一解析

## 注意事项

1. **监控功能需人工验证**：
   - Phase 7.8 需要手动测试确认 5 秒内状态从 Running 更新为 Failed
   - 监控仅检查进程是否存在，不验证实际代理功能
   - 建议结合实际代理测试来获得完整的健康状态

2. **定时器异常处理**：
   - MonitorService 包含 try/catch 防止单次监控异常崩溃整个定时器
   - 但这种设计可能掩盖真实问题，生产环境考虑改进日志记录

3. **代理测试限制**：
   - 当前测试使用固定 URL (http://www.baidu.com)
   - 生产环境考虑使测试 URL 可配置
   - 测试超时固定为 10 秒，可能需要根据网络情况调整

4. **线程安全**：
   - 所有服务设计为线程安全，可从多线程环境安全调用
   - 特别是 MonitorService 使用 Timer 在后台线程运行

## 后续计划（可选）

- 扩展 ProxyDataService 支持多种存储后端（JSON、SQLite、内存等）
- 增强 MonitorService 包含更全面的健康检查（端口监听、实际代理测试等）
- 为 ProxyTester 添加可配置的测试目标和超时时间
- 添加性能监控和指标收集功能
