# 📊 项目进度追踪

> 这是根级入口文件。当前进度详情见 [development/progress.md](development/progress.md)

**最后更新**：2026-07-26

## 最新操作

### 子阶段 1.4：配置迁移工具完善 — 实施完成（2026-07-26）
- ✅ 添加 Version 字段到模型（`AppConfig.Version` / `CurrentVersion = "1.1"`）
- ✅ 实现自动版本升级逻辑（`ProxyDataService.RunUpgradeConfigIfNeeded` — null→1.0→1.1）
- ✅ 增强迁移脚本（`migrate-proxy-data.ps1` — 添加 -TargetVersion 参数、版本合规报告、版本化备份）
- ✅ 添加迁移测试（`ConfigMigrationTests.cs` — 从 5 增至 9 个测试，新增 TestSecurityService）
- ✅ 修复 `PathHelper.cs` XML doc cref 警告（build 0 errors, 0 warnings）
- ✅ 编译验证：0 errors, 0 warnings
- ✅ 测试验证：137 passed, 0 failed（原 128，新增 9）

### 四项优化执行方案 — 实施完成（2026-07-26）
- ✅ 方案 1：`Directory.Build.props` — 添加统一版本号（Version 0.2.0.0, FileVersion 0.2.0.0）
- ✅ 方案 2：新建 `.github/workflows/release.yml` — GitHub Release 自动发布工作流
- ✅ 方案 3：`RealProxyEndToEndTests.cs` 添加 `[Trait("TestCategory", "E2E")]` + CI 中 `e2e-tests` job
- ✅ 方案 4：验证 `.guard/` 目录完整性（7 个文件全部存在）
- ✅ 编译验证：0 errors, 0 warnings
- ✅ 测试验证：127 passed, 0 failed（1 E2E 正确排除）
- ✅ 版本号验证：FileVersion 0.2.0.0

### Phase 4: API 集成到 GUI — 实施完成（2026-07-26）
- ✅ 步骤 4.1：`App.xaml.cs` — 注册 ApiServer 单例到 DI 容器，启动时非阻塞启动 API，退出时兜底停止
- ✅ 步骤 4.2：`MainViewModel.cs` — 注入 ApiServer，添加 ApiStatus/ApiPort 属性，Shutdown 时停止 API
- ✅ 步骤 4.3：`DashboardViewModel.cs` — 添加 ApiStatus/ApiPort 属性和 UpdateApiStatus() 方法
- ✅ 步骤 4.4：`Converters.cs` — 添加 ApiStatusColorConverter（Running → 绿色，Stopped → 灰色）
- ✅ 步骤 4.5：`MainView.xaml` — 仪表盘状态栏添加 API 状态指示器（含圆点、状态文本、端口号）
- ✅ 编译验证：0 errors, 0 warnings
- ✅ 测试验证：128 passed, 0 failed

### Phase 3: 本月内执行方案（优化）- 代码实现完成（2026-07-22）
- ✅ 步骤 3.1：跨平台路径兼容性改进
  - 创建 `src/YLproxy.Utils/PathHelper.cs` — 抽象路径处理工具类（Combine, Normalize, EnsureDirectorySeparator）
  - 替换 6 个文件中的硬编码 `Path.Combine` 调用为 `PathHelper.Combine`
  - 涉及文件：PreFlightChecker.cs, ProxyProcessManager.cs, ConfigGenerator.cs, FileLogger.cs, AutoStartService.cs, AppSettingsService.cs
- ✅ 步骤 3.2：配置管理抽象
  - 创建 `IConfigurationProvider` 接口 + `JsonConfigurationProvider` + `EnvironmentConfigurationProvider` 实现
  - 创建 `ConfigurationManager` — 多源配置管理器（支持缓存、分层覆盖、事件通知）
  - 添加 `Microsoft.Extensions.Configuration` 依赖
- ✅ 步骤 3.3：模块化重构 — DI 注册扩展
  - 创建 `src/YLproxy.Core/DependencyInjection/ServiceCollectionExtensions.cs`
  - 提供 `AddYLproxyServices()`（完整注册）和 `AddYLproxyTestServices()`（测试注册）
  - 注册服务：IConfigurationProvider, ConfigurationManager, IAppSettingsService, ILogger, IProxyDataService, IProxyProcessManager, IProxyTester
- ✅ 步骤 3.4：测试覆盖改进
  - 创建 4 个新的测试文件：PathHelperTests.cs, ConfigurationProviderTests.cs, PerformanceMonitorTests.cs, DependencyInjectionTests.cs
  - 总计新增约 80+ 测试用例，覆盖边缘情况、线程安全、DI 注册验证
- ✅ 步骤 3.5：监控体系建设
  - 创建 `src/YLproxy.Infrastructure/PerformanceMonitor.cs` — 操作计时器 + 聚合统计 + 阈值告警
  - 创建 `src/YLproxy.Infrastructure/Logger.cs` — 结构化日志静态辅助类（Info, Warn, Error, Debug, Fatal + 上下文数据）
  - 集成 `PerformanceMonitor` 到 `MonitorService.cs` 的 MonitorTick 方法
