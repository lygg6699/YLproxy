# 📋 待办任务管理

> 这是根级入口文件。详细任务清单见 [pending/task-tracking.md](pending/task-tracking.md)

**最后更新**：2026-07-26

## 已完成

### 阶段四：用户体验增强（90% → 95%）（2026-07-26）
- [x] 子阶段 4.1：系统托盘功能
  - [x] 安装 Hardcodet.NotifyIcon.Wpf NuGet 包
  - [x] 创建 `TrayIconViewModel.cs` — 托盘图标 ViewModel
  - [x] 更新 `MainWindow.xaml` — 添加 TaskbarIcon + 托盘右键菜单
  - [x] 重构 `MainWindow.xaml.cs` — 替换 WinForms NotifyIcon 为 WPF TaskbarIcon
  - [x] 添加最小化到系统托盘逻辑
- [x] 子阶段 4.2：暗色主题支持
  - [x] 创建 `Themes/LightTheme.xaml` 浅色主题
  - [x] 创建 `Services/ThemeService.cs` 主题管理服务
  - [x] 更新 `App.xaml` — 移除硬编码主题加载，改为运行时动态加载
  - [x] 在 `MainViewModel` 中添加 ToggleThemeCommand
  - [x] 在 `MainView.xaml` 添加主题切换按钮
- [x] 子阶段 4.3：代理分组功能
  - [x] 创建 `GroupViewModel.cs` — 分组数据管理
  - [x] 创建 `ManageGroupsWindow.xaml + .xaml.cs` — 分组管理对话框
  - [x] 创建 `ManageGroupsViewModel.cs` — 对话框 ViewModel
  - [x] 在 `MainViewModel` 中集成分组功能
  - [x] 在 `MainView.xaml` 添加分组过滤 ComboBox + 启动组/停止组按钮
- [x] 编译验证：0 errors, 0 warnings

### 阶段三：CI/CD 自动化（85% → 90%）（2026-07-26）
- [x] 子阶段 3.1：GitHub Actions 发布工作流完善
  - [x] `release.yml` — 添加自动版本号生成、SBOM 生成、覆盖率收集与门槛检查
- [x] 子阶段 3.2：质量门禁加固
  - [x] `ci.yml` — 添加覆盖率收集（coverlet）+ 覆盖率门槛检查（≥80%）
  - [x] `codeql.yml` — CodeQL 安全分析工作流
- [x] 子阶段 3.3：文档自动生成
  - [x] `scripts/generate-docs.ps1` — API 文档静态站点生成脚本
  - [x] `docs.yml` — GitHub Pages 自动部署工作流
  - [x] 删除旧的 `jekyll-gh-pages.yml`

### Phase 4: API 集成到 GUI（2026-07-26）
- [x] `App.xaml.cs` — 注册 ApiServer 单例，启动时 StartAsync，退出时停止
- [x] `MainViewModel.cs` — 注入 ApiServer，添加 API 属性，Shutdown 时停止
- [x] `DashboardViewModel.cs` — 添加 ApiStatus/ApiPort 属性和 UpdateApiStatus()
- [x] `Converters.cs` — 添加 ApiStatusColorConverter
- [x] `App.xaml` — 注册 ApiStatusColorConverter 全局资源
- [x] `MainView.xaml` — 仪表盘状态栏添加 API 状态指示器
- [x] 编译验证：0 errors, 0 warnings
- [x] 测试验证：128 passed, 0 failed

### Phase 3: 本月内执行方案（优化）（2026-07-22）
- [x] 步骤3.1：跨平台路径兼容性改进
  - [x] 创建 `PathHelper.cs`（Combine, Normalize, EnsureDirectorySeparator）
  - [x] 替换 6 个文件中的硬编码 `Path.Combine` 调用
- [x] 步骤3.2：配置管理抽象
  - [x] 创建 `IConfigurationProvider` 接口
  - [x] 创建 `JsonConfigurationProvider` + `EnvironmentConfigurationProvider` 实现
  - [x] 创建 `ConfigurationManager` 多源配置管理器
- [x] 步骤3.3：模块化重构 — DI 注册扩展
  - [x] 创建 `ServiceCollectionExtensions`（`AddYLproxyServices` / `AddYLproxyTestServices`）
- [x] 步骤3.4：测试覆盖改进
  - [x] 新增 4 个测试文件：PathHelperTests, ConfigurationProviderTests, PerformanceMonitorTests, DependencyInjectionTests
  - [x] 新增 80+ 测试用例
- [x] 步骤3.5：监控体系建设
  - [x] 创建 `PerformanceMonitor` 操作计时器
  - [x] 创建 `Logger` 结构化日志辅助类
  - [x] 集成 PerformanceMonitor 到 MonitorService
