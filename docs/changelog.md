# 📝 版本变更历史

> 这是根级入口文件。详细变更历史见 [risks/changelog.md](risks/changelog.md)

**最后更新**：2026-07-22

## 2026-07-22

### Phase 3: 本月内执行方案（优化）— 代码实现完成
- 步骤 3.1：跨平台路径兼容性改进
  - 创建 `PathHelper` 工具类，替换 6 个文件中的硬编码 `Path.Combine`
  - 涉及文件：PreFlightChecker.cs, ProxyProcessManager.cs, ConfigGenerator.cs, FileLogger.cs, AutoStartService.cs, AppSettingsService.cs
- 步骤 3.2：配置管理抽象
  - 创建 `IConfigurationProvider` 接口 + `JsonConfigurationProvider` + `EnvironmentConfigurationProvider` + `ConfigurationManager`
  - 多源配置合并、缓存、事件通知机制
- 步骤 3.3：模块化重构 — DI 注册扩展
  - 创建 `ServiceCollectionExtensions` 提供 `AddYLproxyServices()` / `AddYLproxyTestServices()`
- 步骤 3.4：测试覆盖改进
  - 新增 4 个测试文件、80+ 测试用例（PathHelper, ConfigurationProvider, PerformanceMonitor, DI 注册验证）
- 步骤 3.5：监控体系建设
  - 创建 `PerformanceMonitor`（操作计时器 + 聚合统计 + 阈值告警）
  - 创建 `Logger` 静态日志辅助类（结构化日志 + 上下文数据）
  - 集成 PerformanceMonitor 到 MonitorService
