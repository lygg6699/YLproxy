# 📝 版本变更历史

> 这是根级入口文件。详细变更历史见 [risks/changelog.md](risks/changelog.md)

**最后更新**：2026-08-02

## 2026-08-02

### 阶段二：代码质量提升复核收口
- 完成阶段二落地复核：MainViewModel 子 ViewModel 拆分结构、状态绑定与导入导出状态管理均已在主干生效。
- 完成质量门禁验证：
  - Release 编译通过（`dotnet build YLproxy.sln -c Release`）
  - 非 E2E 自动化测试通过（169 passed, 0 failed）
  - 覆盖率报告已生成（`coverage.cobertura.xml`，line-rate=46.98%）
- 记录风险：历史测试 `PerformanceMonitor_IsThreadSafe` 存在偶发不稳定，需要后续单独治理。

## 2026-07-26

### 阶段四：用户体验增强（90% → 95%）— 实施完成
- ✅ 子阶段 4.1：系统托盘功能（90% → 92%）
  - 安装 `Hardcodet.NotifyIcon.Wpf` NuGet 包
  - 创建 `ViewModels/TrayIconViewModel.cs` — 托盘图标 ViewModel
  - 更新 `MainWindow.xaml` — 添加 `tb:TaskbarIcon` 控件 + 托盘右键菜单
  - 重构 `MainWindow.xaml.cs` — 移除了旧的 WinForms NotifyIcon，替换为纯 WPF TaskbarIcon 方案
  - 创建 `Assets/` 目录（图标资源占位）
  - 添加最小化到系统托盘逻辑（`OnStateChanged` 事件处理）
- ✅ 子阶段 4.2：暗色主题支持（92% → 93%）
  - 创建 `Themes/LightTheme.xaml` — 完整浅色主题（11 种颜色常量 + 12 个控件样式定义）
  - 创建 `Services/ThemeService.cs` — 主题管理服务（单例模式）
  - 更新 `App.xaml` — 移除硬编码的 DarkTheme.xaml 加载，改为 ThemeService 运行时动态加载
  - 在 `MainViewModel` 中添加 ToggleThemeCommand + ToggleTheme() 方法
  - 在 `MainView.xaml` 操作区添加"主题"切换按钮
- ✅ 子阶段 4.3：代理分组功能（93% → 95%）
  - 创建 `ViewModels/GroupViewModel.cs` — 分组数据管理
  - 创建 `Views/ManageGroupsWindow + .xaml.cs` — 分组管理对话框
  - 创建 `ViewModels/ManageGroupsViewModel.cs` — 对话框 ViewModel
  - 在 `MainViewModel` 中集成分组功能（Groups、ShowManageGroupsWindow、StartGroupProxies、StopGroupProxies）
  - 在 `MainView.xaml` 添加分组过滤 ComboBox + "启动组"/"停止组"按钮
- ✅ 编译验证：0 errors, 0 warnings

### 阶段三：CI/CD自动化（85% → 90%）— 实施完成
- ✅ 子阶段 3.1：GitHub Actions 发布工作流完善
  - `release.yml` — 完善：添加自动版本号生成、SBOM 生成、覆盖率收集与门槛检查（≥80%）
- ✅ 子阶段 3.2：质量门禁加固
  - `ci.yml` — 完善：添加覆盖率收集（coverlet）+ 覆盖率门槛检查（≥80%）
  - 新建 `codeql.yml` — CodeQL 安全分析工作流
- ✅ 子阶段 3.3：文档自动生成
  - 新建 `scripts/generate-docs.ps1` — API 文档静态站点生成脚本
  - 新建 `docs.yml` — GitHub Pages 自动部署工作流
  - 删除旧的 `jekyll-gh-pages.yml`
- ✅ 编译验证：0 errors, 0 warnings
- ✅ 文档生成验证：脚本成功运行，生成 `docs/api/index.html`

### 子阶段 1.4：配置迁移工具完善 — 实施完成
- ✅ `ProxyDataService.cs` — `UpgradeConfigIfNeeded` 重命名为 `RunUpgradeConfigIfNeeded`
- ✅ `ConfigMigrationTests.cs` — 从 5 个测试扩展至 9 个
- ✅ `scripts/migrate-proxy-data.ps1` — 添加 `-TargetVersion` 参数、版本合规报告
- ✅ `src/YLproxy.Utils/PathHelper.cs` — 修复 XML doc cref 警告
- ✅ 编译验证：0 errors, 0 warnings
- ✅ 测试验证：137 passed, 0 failed

### 四项优化执行方案 — 实施完成
- ✅ `Directory.Build.props` — 添加统一版本号（Version 0.2.0.0）
- ✅ 新建 `.github/workflows/release.yml` — GitHub Release 自动发布工作流
- ✅ `RealProxyEndToEndTests.cs` — 添加 `[Trait("TestCategory", "E2E")]`
- ✅ 验证 `.guard/` 目录完整性（7 个文件全部存在）
- ✅ 编译验证：0 errors, 0 warnings

### Phase 4: API 集成到 GUI — 实施完成
- ✅ `App.xaml.cs` — 注册 ApiServer 单例到 DI 容器，启动时非阻塞启动 API
- ✅ `MainViewModel.cs` — 注入 ApiServer，添加 ApiStatus/ApiPort 属性
- ✅ `DashboardViewModel.cs` — 添加 ApiStatus/ApiPort 属性和 UpdateApiStatus()
- ✅ `Converters.cs` — 添加 ApiStatusColorConverter
- ✅ `MainView.xaml` — 仪表盘状态栏添加 API 状态指示器
- ✅ 编译验证：0 errors, 0 warnings

## 2026-07-22

### Phase 3: 本月内执行方案（优化）— 代码实现完成
- 步骤 3.1：跨平台路径兼容性改进
  - 创建 `PathHelper` 工具类，替换 6 个文件中的硬编码 `Path.Combine`
- 步骤 3.2：配置管理抽象
  - 创建 `IConfigurationProvider` 接口 + `JsonConfigurationProvider` + `EnvironmentConfigurationProvider` + `ConfigurationManager`
- 步骤 3.3：模块化重构 — DI 注册扩展
  - 创建 `ServiceCollectionExtensions` 提供 `AddYLproxyServices()` / `AddYLproxyTestServices()`
- 步骤 3.4：测试覆盖改进
  - 新增 4 个测试文件、80+ 测试用例
- 步骤 3.5：监控体系建设
  - 创建 `PerformanceMonitor` + `Logger` 静态日志辅助类
  - 集成 PerformanceMonitor 到 MonitorService
