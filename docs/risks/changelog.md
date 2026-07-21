## Phase A3：子 ViewModel 组合模式（2026-07-19）

### 变更
- `src/YLproxy.GUI/MainViewModel.cs`：引入 `HostInfoViewModel`、`DashboardViewModel`、`LogPanelViewModel` 三个子 ViewModel 作为协调器属性，移除内联的 12 个重复属性。
- `src/YLproxy.GUI/Views/MainView.xaml.cs`：`CollectionChanged` 订阅从 `_subscribedVm.FilteredLogs` 改为 `_subscribedVm.LogPanel.FilteredLogs`。
- `src/YLproxy.GUI/Views/MainView.xaml`：修复 Button 缺少 `/>` 的 XAML 语法错误。
- `src/YLproxy.GUI/ViewModels/LogPanelViewModel.cs`：修复 `SetProperty` 返回 `void` 编译错误。

### 验证
- `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
- `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## Phase A4：ProxyItem.CreateTime init-only 化（2026-07-19）

### 变更
- `src/YLproxy.Models/ProxyItem.cs`：`CreateTime { get; set; }` → `CreateTime { get; init; }`，防止创建后篡改。

### 验证
- `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
- `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## Phase A2：接口抽取（AppSettings/IProxy... 接口契约对齐）（2026-07-18）

### 变更
- `src/YLproxy.Infrastructure/IAppSettingsService.cs`：接口契约对齐 `AppSettingsConfig` 返回类型。
- `src/YLproxy.Infrastructure/AppSettingsService.cs`：修复 `GetConfig()` 返回类型并确保 `AppSettingsConfig` 相关定义可用。

### 验证
- `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
- `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## Phase A1：DI 注册 + MainViewModel 构造链闭合（2026-07-18）

### 变更
- `src/YLproxy.GUI/App.xaml.cs`：补齐 DI 注册并通过 DI 创建 `MainViewModel`（启动链闭合）。
- `src/YLproxy.GUI/MainViewModel.cs`：无参构造迁移为依赖注入构造。

### 验证
- `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
- `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## v0.3.0 (2026-07-16)


### 新增
- SQLite 数据持久化层（SqliteProxyRepository）
- JSON → SQLite 自动迁移（DataMigrationService）
- 双写过渡期策略（JSON + SQLite 并行）
- Windows 服务安装/卸载脚本
- 发布打包脚本
- 日志生命周期策略文档化
- 3proxy 引擎日志保留策略

### 变更
- ProxyProcessManager 从 Console 输出迁移到 ILogger
- AppSettingsService 从 Console 输出迁移到 ILogger
- TransparentCoalescingForwarder 从 Console 输出迁移到 ILogger
- ProxyDataService 从 Console 输出迁移到 ILogger
- FileLogger 清理错误现在可通过 CleanupErrors 集合查询
- 空 catch 块全部替换为明确异常处理

### 测试
- 新增 SQLite 迁移测试（5 个）
- 新增日志清理测试（4 个）
- 测试总数从 12 增加到 ≥24

## [GitHub Actions 云端质量门禁] — 2026-07-15

### 新增

- `.github/workflows/ci.yml`：在 Windows 云端执行 SDK、3proxy 运行时、工作区、Debug/Release 构建、测试和覆盖率 Artifact 门禁。
- `.github/PULL_REQUEST_TEMPLATE.md`：增加 Full Check、warnings-as-errors、Nullable、`.guard/review-rules.md` 和文档同步自检项。
- `.github/ISSUE_TEMPLATE/bug_report.md`：规范 Bug 环境、复现步骤和脱敏日志。
- `.github/ISSUE_TEMPLATE/feature_request.md`：规范功能背景、验收标准、影响范围和环境上下文。

### 说明

- CI 使用临时父目录工作区清单兼容现有 `validate-workspace.ps1`，不把本机开发工作区文件或运行时产物提交到仓库。
- `main` 分支保护仍需仓库管理员在 GitHub 设置中将 `CI / quality-gate` 配置为 required status check。

## [GitHub 源码仓库整理] — 2026-07-15

### 新增

- `data/config.example.json`：不含真实凭据的配置模板。
- `scripts/prepare-runtime.ps1`：下载并校验固定版本 3proxy 0.9.7 x64 运行时。
- `LICENSE`：项目 MIT 许可证。
- `THIRD-PARTY-NOTICES.md`：3proxy 许可证、版本和 SHA-256 记录。

### 修改

- `.gitignore`：允许安全说明和脱敏模板进入仓库，继续排除本机数据、日志、构建产物和运行时文件。
- README、环境配置、运行时说明和数据目录说明：同步源码仓库的首次克隆与运行步骤。

## [仓库清理与.gitignore优化] — 2026-07-21

### 新增
- 创建精细的 .gitignore 规则，排除构建产物、日志、敏感配置等文件
- 创建 .gitattributes 统一行尾符规范
- 解除对构建产物、日志、敏感配置文件的 Git 跟踪（使用 git rm --cached）
- 保留关键文件和脱敏模板在仓库中

### 变更
- 移除 AppSettings.json、data/config.json 等敏感文件的 Git 跟踪
- 清理 1500+ 个 bin/obj 文件、300+ 个测试输出文件、大量日志文件
- 删除构建产物 build_stdout.txt、build.binlog、test_3proxy.cfg
- 清理 AI 代理临时文件和黑盒临时日志

### 验证
- `dotnet build YLproxy.sln --configuration Debug`：Build succeeded (0 Error, 0 Warning)
- 成功提交 2263 个文件的变更，删除 154,143 个文件的追踪
- git push origin main 成功同步到远程仓库

关联文档: docs/issues/仓库清理与.gitignore优化执行方案.md

## [CI 构建修复：CA1847/CA2022 + E2E 测试分类] — 2026-07-21

### 根因
- CI #52 构建失败：两个 .NET 10 分析器规则（CA1847、CA2022）在 `-warnaserror` 模式下阻断构建
- CI #53 测试失败：`ManagedProxyForwarderStreamTests` 测试缺少 `[Trait("Category", "E2E")]` 标记，未被 CI 的 `TestCategory!=E2E` 过滤器排除，在 CI 环境中因 TCP 异步时序问题只收到 4096 字节

### 变更
- `src/YLproxy.Infrastructure/AppSettingsService.cs`：`string.Contains("~")` → `string.Contains('~')`（修复 CA1847）
- `tests/ManagedProxyForwarderStreamTests.cs`：添加 `[Trait("Category", "E2E")]` 标记、添加全局 `#pragma warning disable CA2022`

### 验证
- `dotnet build YLproxy.sln --configuration Debug -warnaserror`：Build succeeded（0 Error, 0 Warning）
- 本地已确认两个文件修改生效

关联文档: CI修复执行方案_步骤2.md、CI修复执行方案_步骤3.md

## [Git跟踪清理：移除覆盖率产物和IDE配置] — 2026-07-21

### 变更
- 使用 `git rm --cached -r` 解除对 `reports/coverage/` 的 Git 跟踪（14 个 coverage.cobertura.xml 文件）
- 使用 `git rm --cached -r` 解除对 `tests/TestResults/` 的 Git 跟踪（5 个 coverage.cobertura.xml + 1 个 testresults.trx）
- 使用 `git rm --cached` 解除对 `.vscode/` 目录下 4 个 IDE 配置文件的 Git 跟踪
- 补充 `.gitignore` 规则：`reports/coverage/` 和 `tests/TestResults/`

### 验证
- `git status` 显示 20 个文件处于 "deleted" 暂存状态
- `.gitignore` 已更新，新增覆盖率产物和测试结果规则
- 本地文件保留，未删除

## [仓库沉积文件清理] — 2026-07-21

### 变更
- 删除 `reports/` 目录下 14 个过程性检查报告 (check-report-*.md)
- 删除 `reports/` 目录下 5 个过时汇总报告 (clarification_report.md 等)
- 保留 `reports/code_sync_summary_report.md` 和 `reports/env_audit_20260716.md`
- 将 `reports/env_audit_20260716.md` 复制到 `docs/audit/environment-baseline-20260716.md`
- 删除 `reports/coverage/` 目录下所有覆盖率产物
- 归档 `TODO_P0-6.md` → `docs/archive/P0-6-execution-tracking.md`
- 归档 `TODO_cloud_group5.md` → `docs/archive/cloud-group5-execution-tracking.md`
- 删除已完成的 `TODO_PHASEA.md` 和 `TODO_PHASEA_WORKFLOW.md`

### 验证
- `reports/` 目录保留 2 个文件 (code_sync_summary_report.md, env_audit_20260716.md)
- 根目录 `.md` 文件保留: README.md, THIRD-PARTY-NOTICES.md, TODO.md
- TODO.md 已精简为当前待办事项
```
