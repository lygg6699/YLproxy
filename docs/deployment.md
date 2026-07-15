## 本地 .NET 分析器、警告门禁、覆盖率与发布验证（2026-07-15）

**部署状态：** 已完成

### 变更内容

- 新增根目录 `Directory.Build.props`，统一配置 Nullable、ImplicitUsings、Roslyn 推荐分析级别和编译时代码风格检查。
- Release 构建启用 `TreatWarningsAsErrors=true`。
- Full Check 增加 `win-x64` framework-dependent 发布验证。
- Full Check 测试阶段增加 `XPlat Code Coverage`，覆盖率输出到 `reports/coverage`。
- 工作区校验脚本增加构建属性和 Release 警告门禁检查。
- 为满足新的 Release 门禁，补齐了日志文化设置、资源释放、参数校验、配置生成和测试命名问题。

### 验证结果

- Release 构建：通过，0 Warning / 0 Error。
- Release 发布：通过，发布目录和 `YLproxy.GUI.exe` 存在。
- 覆盖率收集：覆盖率文件成功生成。
- 完整测试：通过，12/12。
- Smoke Test：通过，隔离 GUI 启动、日志检查、进程关闭和临时目录清理全部完成。

## P2 日志清理与 MonitorService 鲁棒性加固（2026-07-15）

**部署状态：** 已完成

### 变更内容

- FileLogger 初始化时异步清理 `logs/` 下超过 `RetentionDays` 的 `.log` 和 `.txt` 文件。
- 日志清理依据 `LastWriteTimeUtc`，避免文件复制或创建时间变化造成误删。
- MonitorService 对单代理进程检测异常执行隔离处理并记录 Warning，Timer 不因单次检测失败停止。
- Stop 对进程已被系统移除的情况执行正常清理，不重复抛出异常。
- 配置、日志、启动和进程清理降级路径补充诊断输出。

### 验证结果

- Release 构建：0 Warning / 0 Error。
- 日志清理回归测试：通过。
- MonitorService 异常连续运行测试：通过。
- Stop 缺失进程测试：通过。
- Full Check：15/15 测试通过，Smoke Test 通过。

## DPAPI 跨机器解密容错与凭据重置（2026-07-15）

**部署状态：** 已完成

### 行为变更

- DPAPI 密文属于其他 Windows 用户或机器时，配置加载不再导致应用启动崩溃。
- 受影响代理的用户名和密码会被清空，状态重置为 `Stopped`。
- 配置通过 `ProxyDataService` 的原子保存流程写回，避免继续保留不可用密文。
- GUI 日志输出：`Warning: Local credentials could not be decrypted on this machine. Please re-enter the password.`

### 验证结果

- 不可解密密文回归测试：通过。
- 完整测试：12/12 通过。
- Release 构建：0 Warning / 0 Error。

## GitHub 源码仓库整理与首次上传（2026-07-15）

**状态：本地整理完成，待验证后推送**

### 发布边界

- 源代码、测试、脚本、文档、项目规范和配置模板进入 GitHub 仓库。
- `data/config.json`、根 `logs/`、`reports/`、构建输出和 3proxy 生成 cfg/日志只保留在本机。
- 3proxy Windows x64 二进制不进入 Git；首次克隆后执行 `scripts/prepare-runtime.ps1`。
- 运行时版本固定为 3proxy 0.9.7，脚本会校验官方 Release 压缩包 SHA-256。
- `runtime/3proxy/copying` 和 `THIRD-PARTY-NOTICES.md` 保留第三方许可证与归属信息。

### 推送前门禁

1. 执行 `git diff --check`。
2. 执行 `dotnet build YLproxy.sln --configuration Debug`。
3. 执行 `dotnet test tests/YLproxy.Tests.csproj --configuration Debug`。
4. 执行 `scripts/validate-workspace.ps1` 和 `scripts/full-check.ps1`。
5. 确认 `git ls-files` 中没有 `data/config.json`、日志、运行时二进制或生成 cfg。

## 剩余风险闭环：DPAPI 与真实 3proxy parent 链

**部署时间：** 2026-07-15

**授权来源：** 用户明确要求修复剩余风险问题。

### 安全存储

- `DpapiSecurityService` 使用 Windows DPAPI `DataProtectionScope.CurrentUser`。
- 用户名和密码在 `data/config.json` 中使用 `dpapi:v1:` 前缀密文保存，加载时仅在内存中解密。
- 旧明文配置通过 `scripts/migrate-proxy-data.ps1 -Apply` 迁移；脚本会临时备份，启动失败或校验失败时恢复原文件。
- 当前 2 条代理记录已完成迁移，迁移备份已删除，真实配置未输出到日志。

### 3proxy 转发链

- 上游转发使用 `parent 1000 http HOST PORT [USER PASSWORD]`，不再把远程地址错误地放入 `-e` 出口参数。
- 使用 `fakeresolve` 避免目标域名在本机解析后绕过上游代理。
- 生成 cfg 仅在 3proxy 运行期间保留；启动失败、正常停止和监控发现进程退出时删除。
- 自动化测试使用本机伪造的认证 HTTP parent，验证本地端口、认证头、响应转发和 cfg 清理。

### 验证结果

- 构建：0 Error，0 Warning。
- 测试：10 Passed，0 Failed。
- 当前配置明文门禁：通过。
- 真实外部供应商验收：仍需在目标网络和实际凭据下执行，不以本机伪造 parent 结果替代。

## P0/P1/P2 工作区与验证链风险加固

**部署时间：** 2026-07-15

**授权来源：** 用户明确要求按风险建议执行。

**部署范围：** 更新 Full Check 脚本、父目录工作区任务和 SDK 环境约束；新增工作区校验脚本与 `global.json`。未修改真实代理数据、生产配置内容或既有未完成待办。

### 关键变更

- `scripts/full-check.ps1` 以项目根目录为工作目录，使用 `src/YLproxy.GUI/bin/Debug/net10.0-windows/YLproxy.GUI.exe`。
- Smoke Test 在系统临时目录创建隔离项目副本，使用 `{"Proxies":[]}` 测试数据，避免触碰根目录 `data/config.json`。
- Smoke Test 使用 10 秒启动轮询，并在成功、失败和异常路径统一关闭进程、删除临时目录。
- Full Check 的旧编译清理失败会自动重试，重试仍失败则终止检查。
- 新增 `scripts/validate-workspace.ps1`，校验父目录工作区、解决方案、SDK、GUI/测试项目和 3proxy 依赖。
- 新增 `global.json`，固定 SDK `10.0.301` 并允许同一特性带内的最新补丁版本。
- 父目录工作区新增安全 Full Check、显式 Smoke Test 和环境校验任务；`reports/` 加入 `.gitignore`。

### 验证结果

- 环境校验：通过，SDK `10.0.301`、工作区 JSON、工程入口和 3proxy 依赖均有效。
- Full Check：通过，清理成功、构建 0 Error/0 Warning、测试 7 Passed。
- 隔离 Smoke Test：通过，GUI 启动成功，隔离日志无错误，进程和临时目录已清理。
- 真实 `data/config.json`：未被 Smoke Test 读取或修改。

## 独立 VS Code 工作区配置

**部署时间：** 2026-07-15

**授权来源：** 用户明确回复“开始执行”后执行。

**部署范围：** 在 `E:\GZQ\YLXCX` 新增 `YLproxy.code-workspace`，不修改源代码、用户代理数据、3proxy 运行时或项目内既有 `.vscode` 文件。

### 配置内容

- 将 `YLproxy/` 设置为唯一工作区文件夹。
- 统一 UTF-8 编码、Windows .NET CLI 英文输出和搜索/文件监听排除规则。
- 提供 Restore、Debug/Release 构建、测试、GUI 运行、Full Check 和 Clean 任务。
- 提供 `YLproxy.GUI` 的 .NET `coreclr` 调试配置。
- 推荐 C# Dev Kit、PowerShell 和简体中文语言包扩展。

### 验证结果

- 工作区 JSON 结构校验：通过。
- `dotnet build YLproxy.sln --configuration Debug`：成功，0 Error，0 Warning。
- `dotnet test tests/YLproxy.Tests.csproj --configuration Debug --no-build --no-restore`：7 Passed，0 Failed。

## 配置唯一性与 C# 配置归一

**部署时间：** 2026-07-15

**验证命令：** `dotnet build YLproxy.sln --no-restore`；`dotnet test tests/YLproxy.Tests.csproj --no-restore`

**修改内容：** 全局配置服务重命名为 `AppSettingsService`，根配置模型重命名为 `AppSettingsConfig`，删除未使用接口和更新入口，强制配置只能落在规范路径。

**保留项：** `data/config.json` 和 `runtime/3proxy/cfg` 未删除。
## 配置一致性治理

**部署时间：** 2026-07-15


## Phase 2 — MVVM 静态 GUI 基础结构

**部署时间：** 2026-07-13 12:09

**部署环境：** Windows 10, .NET 10.0

**构建命令：** `dotnet build YLproxy.sln`

**构建结果：** 成功（0 Error, 0 Warning）

**修改内容：**
- 新增 YLproxy.GUI/MainViewModel.cs
- 新增 YLproxy.GUI/Views/MainView.xaml

## 部署沉积清理与基线验证

**执行时间：** 2026-07-15

### 清理内容

- 删除根目录、各项目和 `path_verif` 下的 `bin/`、`obj/` 构建输出及 MSBuild/NuGet 缓存。
- 删除根 `logs/` 中的历史应用日志。
- 删除 `runtime/3proxy/logs/` 中的 3proxy 引擎历史日志。
- 删除 `.blackbox/tmp/` 中的部署命令临时日志。
- 删除 `src/YLproxy.GUI/logs/` 重复运行日志目录。

### 保留内容

- 保留根 `logs/README.md` 说明文件。
- 保留 `AppSettings.json` 和 `data/config.json`。
- 保留 `runtime/3proxy/bin64/` 第三方运行时和 `runtime/3proxy/cfg/` 模板/运行配置。
- 不触碰 `.git/`、源代码、测试源文件和文档历史。

### 验证结果

- `dotnet build YLproxy.sln`：成功，0 Error，0 Warning。
- `dotnet test tests/YLproxy.Tests.csproj`：7 Passed，0 Failed。
- 清理后断言通过：目标缓存与历史日志不存在，必要运行资产完整。

### 后续部署门禁

在进入对外发布前，仍需完成真实 3proxy 端到端验收和 DPAPI 凭据加密；当前清理不等同于发布验收，也不删除现有用户代理数据。
## Phase 9 — 终端乱码修复与编码统一

**部署时间：** 2026-07-15 06:16

**部署环境：** Windows 10/11, .NET 10.0

**构建命令：** `dotnet build YLproxy.sln`

**测试命令：** `dotnet test tests/YLproxy.Tests.csproj`

**验证结果：**
- 构建成功（0 Error, 0 Warning）
- 测试通过（2 Passed, 0 Failed）

**修改内容：**
- 新增 `.editorconfig`：统一文本文件 UTF-8 编码策略
- 新增 `.vscode/settings.json`：固定编辑器编码与 dotnet 终端语言
- 更新 `.blackboxrules`：固定 dotnet 命令前置语言环境变量
- 修复 `.blackbox/tmp/shell_tool_0b6f357b97b3.log` 已乱码文本
- 更新 `README.md`：补充 Windows 终端乱码规避说明
