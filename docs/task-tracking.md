## Phase A1：DI 注册 + MainViewModel 构造链闭合（2026-07-18）

**状态：已完成（build/test 复验通过）**

- [x] 修改 `src/YLproxy.GUI/App.xaml.cs`：补齐 DI 注册并通过 DI 创建 `MainViewModel`（使用 `ServiceLocator` 初始化 provider）。
- [x] 修改 `src/YLproxy.GUI/MainViewModel.cs`：`MainViewModel()` 无参构造迁移为带依赖注入构造（ILogger / AppSettingsService / ProxyConfig / ThreeProxyConfig）。
- [x] 验证复验：
  - `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
  - `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## Phase A2：接口抽取（AppSettings/IProxy... 接口契约对齐）（2026-07-18）

**状态：已完成（关键编译闭环）**

- [x] `IAppSettingsService.GetConfig()` 与 `AppSettingsConfig` 类型对齐（修复返回类型不一致导致的转换失败/编译阻断）。
- [x] 验证复验：
  - `dotnet build YLproxy.sln`：Build succeeded（warnings 不阻断）。
  - `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E`：total 75, failed 0, succeeded 75。

## Phase 2.5 代理认证与网络连接修复（2026-07-15）


**任务：** 修复带账号密码代理无法连接网络的问题，并同步更新单元测试与文档

**状态：✅ 已完成**

- [x] 修复 `TransparentCoalescingForwarder` 逻辑，确保在传入请求完全不带任何认证头时，也能正确注入真实的 Proxy-Authorization 凭据。
- [x] 新增单元测试 `ForwarderShouldInjectAuthHeaderEvenIfIncomingRequestHasNoAuth` 验证该修复方案。
- [x] 更新 docs/progress.md 和 docs/task-tracking.md 详细记录 Phase 2.5 修复方案与验证结果。

## Job Object 孤儿进程防护与 CI 加固（2026-07-15）

**任务：** 加固 GitHub Actions CI 工作流，隔离物理网络依赖测试，并新增 Job Object 孤儿进程防护追踪

**状态：✅ 已完成**

- [x] 编辑 `.github/workflows/ci.yml`，在 `dotnet test` 步骤中通过 `--filter` 隔离依赖物理网络/DPAPI 的集成测试。
- [x] 确保 CI 流程中包含 `dotnet build` Debug/Release 且开启警告转错误（`-warnaserror`）门禁。
- [x] 规划并追踪 Job Object 孤儿进程防护机制。

## GitHub Actions 云端质量门禁（2026-07-15）

**任务：** 配置 Windows 云端 CI、PR/Issue 模板和 `main` 分支质量门禁

**状态：仓库文件已完成，远端分支保护待管理员权限**

- [x] 创建 `.github/workflows/ci.yml`，固定 `windows-latest` 和 `global.json` SDK。
- [x] 接入 3proxy 运行时准备、工作区环境校验、Debug/Release warnings-as-errors 构建。
- [x] 接入 Debug 测试、覆盖率 XML 和测试结果 Artifact 上传。
- [x] 创建 PR 质量自检模板。
- [x] 创建 Bug 和功能申请 Issue 模板。
- [ ] 配置 `main` 仅允许 PR 合并并要求 `CI / quality-gate` 通过。

## GitHub 源码仓库整理与首次上传

**任务：** 整理公开仓库边界并上传到 `lygg6699/YLproxy`

**状态：本地整理已完成，等待验证与推送**

- [x] 排除真实代理数据、日志、报告、构建产物和运行时生成文件
- [x] 新增脱敏配置模板、MIT 许可证和第三方依赖声明
- [x] 新增固定 3proxy 0.9.7 x64 下载、哈希校验和运行时准备脚本
- [x] 同步 README、环境配置和 3proxy 运行时说明
- [ ] 完成本地构建、测试、提交审计和远端 `main` 推送

## 剩余风险闭环：DPAPI 与真实 3proxy parent 链（2026-07-15）

**任务：** 消除凭据明文落盘风险并完成可复现的 3proxy 上游转发验收

**状态：核心实现已完成，外部环境验收待执行**

- [x] 使用 Windows DPAPI `CurrentUser` 加密用户名和密码
- [x] 自动识别旧明文配置并迁移，迁移失败支持临时备份恢复
- [x] 将 3proxy 上游链路改为 `parent http` 并启用 `fakeresolve`
- [x] 清理启动失败、停止和异常退出后的敏感 cfg
- [x] 新增 DPAPI/迁移序列化测试和真实 3proxy 本地 parent 集成测试
- [x] 迁移当前 2 条 `data/config.json` 记录并删除旧明文 cfg
- [x] 环境校验增加明文凭据门禁
- [ ] 使用真实外部上游代理完成目标网络单代理、多代理和故障回滚验收

## P0/P1/P2 工作区与验证链风险加固（2026-07-15）

**任务：** 修复 Smoke Test 执行风险、隔离真实用户数据并统一开发环境

**状态：已完成**

- [x] 修复 Full Check 项目根目录、GUI 输出路径和清理失败处理
- [x] 将 Smoke Test 迁移到临时项目副本，使用空代理配置并保证进程/目录清理
- [x] 保留父目录工作区作为单机入口，增加工作区 JSON 和关键路径校验脚本
- [x] 增加 `global.json` 固定 .NET SDK 版本和 `latestPatch` 策略
- [x] 增加环境校验任务和显式 Smoke Test 工作区任务
- [x] 将验证报告目录加入 `.gitignore`，避免验证产物混入既有脏改动
- [x] 完成环境校验、完整构建、7 项单元测试和隔离 Smoke Test

## XAML 编译和代码质量修复（2026-07-15）

**状态：✅ 已完成**

- [x] 执行 `dotnet clean` 清理构建输出
- [x] 执行 `dotnet restore` 恢复 NuGet 包
- [x] 执行 `dotnet build` 重新生成 XAML .g.cs 文件
- [x] 修复 ExceptionHandler.cs 的 null reference 警告
- [x] 构建零警告验证
- [x] 单元测试验证（7/7 passed）

## 项目根目录清理（2026-07-15）

**任务：** 清理和规范化项目根目录配置文件

**状态：** 已完成

- [x] 删除 YLproxy.slnx（自动生成文件）
- [x] 删除 test_path.cs（临时测试文件）
- [x] 确认 YLproxy.sln 保留为主要解决方案文件
- [x] 确认 AppSettings.json 保留为全局运行配置
- [x] 更新 .gitignore 补充缺失规则

---

## 运维部署文档优化（2026-07-15）

**任务：** Phase E — 优化 `development-deployment-outline` 文档，增加统一 API 部署规范和用户端功能使用逻辑

**状态：** 已完成

- [x] 创建 API 部署规范文档（部署架构、系统要求、部署步骤、配置管理、API 端点定义）
- [x] 创建用户使用手册（界面介绍、基础操作、代理管理、进阶功能、常见场景、最佳实践）
- [x] 创建部署流程文档（部署前检查、5 个阶段详细步骤、验证清单、回滚策略、灾难恢复）
- [x] 更新 README.md 文档导航索引
- [x] 更新文档版本日志
- [x] 完成 Phase E 运维部署文档建设

---

## 配置唯一性与 C# 配置归一（2026-07-15）

- [x] 盘点所有活动 JSON 和 C# JSON 读写入口
- [x] 统一全局配置服务命名为 `AppSettingsService`
- [x] 统一根配置模型命名为 `AppSettingsConfig`
- [x] 删除未使用的 `IConfigService` 和 `UpdateSection`
- [x] 验证根配置与代理数据配置路径唯一性
- [x] 完成构建、测试、引用和目录扫描
## 配置一致性治理（2026-07-15）

- [x] 修正 `.agent` 项目目录树与唯一规则文件名
- [x] 统一全局配置校验和日志配置读取路径
- [x] 将代理数据服务统一为 `ProxyDataService`
- [x] 强制代理数据只能写入 `data/config.json`
- [x] 增加配置键、规范目录和错误路径回归测试
- [x] 保留本地代理数据与 runtime cfg，完成构建和测试验证

## 部署沉积清理与执行方案（2026-07-15）

**状态：已完成**

- [x] 重构根目录 `TODO.md`，记录已部署能力、清理边界、后续阶段和完成标准。
- [x] 删除构建缓存、临时验证输出、历史应用日志、3proxy 引擎日志和黑盒临时日志。
- [x] 保留用户配置、3proxy 二进制、模板及当前运行 cfg。
- [x] 完成干净构建和测试验证：构建 0 Error/0 Warning，测试 7 Passed/0 Failed。

### 当前待办转入

- [ ] P0：完成 3proxy 真实环境端到端验收，并形成可复现记录。
- [ ] P1：完成 DPAPI 凭据加密、旧明文配置迁移和安全回归测试。
- [ ] P2：补齐日志清理回归测试、ILogger 调用点审计和异常处理治理。

## P2 日志、异常与数据可靠性（2026-07-16）

**任务：** 统一 ILogger 输出、治理异常处理、SQLite 数据层实现

**状态：执行中**

- [ ] 1. ProxyProcessManager 接入 ILogger（33 处 Console 清理）
- [ ] 2. AppSettingsService 接入 ILogger（5 处）
- [ ] 3. TransparentCoalescingForwarder + ProxyDataService 接入 ILogger（2 处）
- [ ] 4. FileLogger 自清理路径修复
- [ ] 5. 空 catch 块治理
- [ ] 6. 日志生命周期文档更新 + 3proxy 引擎日志清理
- [ ] 7. 测试补齐（预期 ≥7 个新测试）
- [ ] 8. SQLite 数据层实现
- [ ] 9. SQLite 迁移单元测试
- [ ] 10. ProxyDataService 改造
- [ ] 11. 安装脚本与部署工具（install-service.ps1 等）
- [ ] 12. docs/progress.md / docs/task-tracking.md / docs/deployment.md / docs/changelog.md 同步
## 独立 VS Code 工作区配置（2026-07-15）

**任务：** 为 `YLproxy` 创建父目录级独立工作区配置

**状态：已完成**

- [x] 在 `E:\GZQ\YLXCX` 创建 `YLproxy.code-workspace`
- [x] 配置 `YLproxy/` 单项目工作区及统一编辑器环境
- [x] 配置 Restore、Build、Test、Run GUI、Full Check 和 Clean 任务
- [x] 配置 WPF GUI `coreclr` 调试入口
- [x] 完成工作区 JSON、解决方案构建和单元测试验证
- [x] 同步 progress、deployment、changelog 追踪记录

# 当前任务

**任务：** Phase 2.1 — 运行链端到端验收与点击反馈交互修复

**状态：** 已完成

**完成时间：** 2026-07-15 06:40

---

## Todo

- [x] 10.1 开展 Phase 2.1 端到端全链路功能盘点与摸排
- [x] 10.2 复现并定位“UI 代理行无法正常选择”与“点击选中丢失高亮反馈”问题 
- [x] 10.3 精细重构 `src/YLproxy.GUI/App.xaml` 中的 `DataGridRow` / `DataGridCell` 样式，增加 VS 暗夜系色彩联动，获得极佳点击反馈
- [x] 10.4 复现并定位“测试/启动/停止按钮首次点击正常，第二次点击彻底失效”的核心 Bug 
- [x] 10.5 修复 `src/YLproxy.GUI/MainViewModel.cs` 中 `IsTesting`、`IsStarting`、`IsStopping` 标志在各种场景出口不归零的 Bug（引入 `try-finally` 防御）
- [x] 10.6 构建与单元测试全面验证，确认 regression 测试均完美通过
- [x] 10.7 输出完整的端到端 9 项验收文档 [docs/acceptance/Phase-2.1-E2E-Acceptance.md](docs/acceptance/Phase-2.1-E2E-Acceptance.md)
- [x] 10.8 全面更新 `progress.md`、`task-tracking.md` 以及 `changelog.md` 变更历史
