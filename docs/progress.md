# 📊 项目进度追踪

> 这是根级入口文件。当前进度详情见 [development/progress.md](development/progress.md)

**最后更新**：2026-08-02

## 最新操作

### 阶段二：代码质量提升复核收口（2026-08-02）
- ✅ 子阶段 2.1 结构复核：MainViewModel 子 ViewModel 拆分架构已在主干落地
  - 操作状态绑定使用 `ProxyOperations.IsTesting/IsStarting/IsStopping`
  - 导入导出状态绑定使用 `ImportExport.IsExporting/IsImporting`
- ✅ 子阶段 2.2 测试与覆盖率复核：
  - Release 编译通过（0 error）
  - 非 E2E 测试通过：169 passed, 0 failed
  - 覆盖率基线（非 E2E）：line-rate = 46.98%（`coverage.cobertura.xml`）
- ✅ 子阶段 2.3 清理复核：
  - ServiceLocator 未发现残留引用
  - 阶段门禁可通过（串行执行下无文件锁）
- ⚠️ 风险记录：历史测试 `PerformanceMonitor_IsThreadSafe` 存在偶发不稳定，需要在后续阶段单独治理

### 阶段四：用户体验增强（90% → 95%）— 实施完成（2026-07-26）
- ✅ 子阶段 4.1：系统托盘功能（90% → 92%）
  - 安装 Hardcodet.NotifyIcon.Wpf NuGet 包
  - 创建 `TrayIconViewModel.cs` — 托盘图标 ViewModel
  - 更新 `MainWindow.xaml` — 添加 TaskbarIcon + 托盘右键菜单（Hardcodet.Wpf 纯 WPF 方案）
  - 重构 `MainWindow.xaml.cs` — 移除了旧的 WinForms NotifyIcon，替换为 WPF TaskbarIcon
  - 添加最小化到系统托盘逻辑
- ✅ 子阶段 4.2：暗色主题支持（92% → 93%）
  - 创建 `Themes/LightTheme.xaml` — 完整浅色主题（11 种颜色常量，12 个控件样式）
  - 创建 `Services/ThemeService.cs` — 主题管理服务（ApplyTheme/ToggleTheme/ApplySystemTheme）
  - 更新 `App.xaml` — 移除硬编码的 DarkTheme.xaml 加载，改为 ThemeService 运行时动态加载
  - 在 `MainViewModel` 中添加 ToggleThemeCommand + ToggleTheme() 方法
  - 在 `MainView.xaml` 操作区添加"主题"切换按钮
- ✅ 子阶段 4.3：代理分组功能（93% → 95%）
  - 创建 `GroupViewModel.cs` — 分组数据管理
  - 创建 `ManageGroupsWindow.xaml + .xaml.cs` — 分组管理对话框
  - 创建 `ManageGroupsViewModel.cs` — 对话框 ViewModel
  - 在 `MainViewModel` 中集成分组功能
  - 在 `MainView.xaml` 添加分组过滤 ComboBox + "启动组"/"停止组"按钮
- ✅ 编译验证：0 errors, 0 warnings

### 阶段三：CI/CD自动化（85% → 90%）— 实施完成（2026-07-26）
- ✅ 子阶段 3.1：GitHub Actions 发布工作流完善（85% → 87%）
  - 完善 `release.yml` — 添加自动版本号生成、SBOM 生成、覆盖率收集与门槛检查
- ✅ 子阶段 3.2：质量门禁加固（87% → 88%）
  - 完善 `ci.yml` — 添加覆盖率收集（coverlet）+ 覆盖率门槛检查（≥80%）
  - 新建 `codeql.yml` — CodeQL 安全分析工作流
- ✅ 子阶段 3.3：文档自动生成（88% → 90%）
  - 创建 `scripts/generate-docs.ps1` — API 文档静态站点生成脚本
  - 新建 `docs.yml` — GitHub Pages 自动部署工作流
  - 删除旧的 `jekyll-gh-pages.yml`
- ✅ 编译验证：0 errors, 0 warnings

### 子阶段 1.4：配置迁移工具完善 — 实施完成（2026-07-26）
- ✅ 添加 Version 字段到模型（`AppConfig.Version` / `CurrentVersion = "1.1"`）
- ✅ 实现自动版本升级逻辑
- ✅ 增强迁移脚本
- ✅ 编译验证：0 errors, 0 warnings
- ✅ 测试验证：137 passed, 0 failed（原 128，新增 9）

### 四项优化执行方案 — 实施完成（2026-07-26）
- ✅ 方案 1：`Directory.Build.props` — 添加统一版本号
- ✅ 方案 2：新建 `.github/workflows/release.yml`
- ✅ 方案 3：`RealProxyEndToEndTests.cs` 添加 `[Trait("TestCategory", "E2E")]`
- ✅ 方案 4：验证 `.guard/` 目录完整性
- ✅ 编译验证：0 errors, 0 warnings

### Phase 4: API 集成到 GUI — 实施完成（2026-07-26）
- ✅ `App.xaml.cs` — 注册 ApiServer 单例、启动时 StartAsync、退出时停止
- ✅ `MainViewModel.cs` — 注入 ApiServer，添加 ApiStatus/ApiPort 属性
- ✅ `DashboardViewModel.cs` — 添加 ApiStatus/ApiPort 属性和 UpdateApiStatus()
- ✅ `Converters.cs` — 添加 ApiStatusColorConverter
- ✅ 编译验证：0 errors, 0 warnings
- ✅ 测试验证：128 passed, 0 failed

### Phase 3: 本月内执行方案（优化）- 代码实现完成（2026-07-22）
- ✅ 步骤 3.1：跨平台路径兼容性改进
- ✅ 步骤 3.2：配置管理抽象
- ✅ 步骤 3.3：模块化重构 — DI 注册扩展
- ✅ 步骤 3.4：测试覆盖改进（新增 80+ 测试用例）
- ✅ 步骤 3.5：监控体系建设
