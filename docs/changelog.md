# 📝 版本变更历史

> 这是根级入口文件。详细变更历史见 [risks/changelog.md](risks/changelog.md)

**最后更新**：2026-07-26

## 2026-07-26

### 子阶段 1.4：配置迁移工具完善 — 实施完成
- ✅ `ProxyDataService.cs` — `UpgradeConfigIfNeeded` 重命名为 `RunUpgradeConfigIfNeeded`（public），添加版本升级扩展点
- ✅ `ConfigMigrationTests.cs` — 修复测试调用，从 5 个测试扩展至 9 个（含幂等性、未来版本兼容、序列化回环验证）
- ✅ `scripts/migrate-proxy-data.ps1` — 添加 `-TargetVersion` 参数、版本合规报告、版本化备份机制
- ✅ `src/YLproxy.Utils/PathHelper.cs` — 修复 XML doc cref 警告（CA1200）
- ✅ `tests/` — 添加 `TestSecurityService`（内联 mock），消除 DPAPI 平台依赖对序列化测试的限制
- ✅ 编译验证：0 errors, 0 warnings
- ✅ 测试验证：137 passed, 0 failed（原 128，新增 9）

### 四项优化执行方案 — 实施完成
- ✅ 方案 1：`Directory.Build.props` — 添加统一版本号（Version 0.2.0.0, FileVersion 0.2.0.0, InformationalVersion 0.2.0+sha）
- ✅ 方案 2：新建 `.github/workflows/release.yml` — GitHub Release 自动发布工作流
- ✅ 方案 3：`RealProxyEndToEndTests.cs` 添加 `[Trait("TestCategory", "E2E")]` + CI 中 `e2e-tests` job
- ✅ 方案 4：验证 `.guard/` 目录完整性（7 个文件全部存在）
- ✅ 编译验证：0 errors, 0 warnings
- ✅ 测试验证：127 passed, 0 failed（1 E2E 正确排除）
- ✅ 版本号验证：FileVersion 0.2.0.0

### Phase 4: API 集成到 GUI — 实施完成
- ✅ `App.xaml.cs` — 注册 ApiServer 单例到 DI 容器，启动时非阻塞启动 API，OnExit 时兜底停止
- ✅ `MainViewModel.cs` — 注入 ApiServer，添加 ApiStatus/ApiPort 属性，ShutdownAsync 时停止 API
- ✅ `DashboardViewModel.cs` — 添加 ApiStatus/ApiPort 属性和 UpdateApiStatus() 方法
- ✅ `Converters.cs` — 添加 ApiStatusColorConverter（Running→绿色，Stopped→灰色）
- ✅ `App.xaml` — 注册 ApiStatusColorConverter 为全局资源
- ✅ `MainView.xaml` — 仪表盘状态栏添加 API 状态指示器（圆点+状态文本+端口号）
- ✅ 编译验证：0 errors, 0 warnings
- ✅ 测试验证：128 passed, 0 failed

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
