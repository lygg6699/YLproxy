# Phase A 补齐执行清单（DI + God Class 拆分 A1~A4）

> 目标：v0.2.0 → v0.3.0 的 Phase A（架构基础重构），实现可运行、可编译、可测试、可验收，并同步文档。

## A0 前置（已开始执行/会在每步验证）
- [x] 约束：`dotnet build YLproxy.sln` 通过
- [x] 约束：`dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E` 通过



## A1 DI 容器与启动链闭合
- [x] 1) `App.xaml.cs`：补齐部分 DI 注册（加入 `ISecurityService=DpapiSecurityService`（Windows only），为后续 DI 链闭环做前置准备）


- [ ] 2) `MainWindow.xaml.cs`：由 DI 解析 ViewModel/窗口，移除任何 `new MainViewModel()` 的依赖链
- [ ] 3) `ServiceLocator`：仅作为过渡层（如保留需明确用途；最终目标是减少依赖）


## A2 接口抽取（按目标文件结构落地）
- [ ] 1) 新增 Core Abstractions 接口文件：
  - [ ] IProxyDataService.cs
  - [ ] IProxyTester.cs
  - [ ] IProxyRepository.cs
- [ ] 2) 新增 Proxy Abstractions：
  - [ ] IProxyProcessManager.cs
- [ ] 3) 新增 Infrastructure Abstractions：
  - [ ] IAppSettingsService.cs
- [ ] 4) 现有实现类实现对应接口并替换 ViewModel 依赖。

## A3 MainViewModel God Class 拆分
- [ ] 1) 新增子 VM：
  - [ ] HostInfoViewModel
  - [ ] DashboardViewModel
  - [ ] LogPanelViewModel
- [ ] 2) 新增 ProxyOperation/Management（按你要求“纯 Service，不含 UI 绑定逻辑”）
- [ ] 3) `MainViewModel`：降为协调器（组合子 VM + 暴露 Proxies 集合）
- [ ] 4) `MainView.xaml`：绑定路径改为子属性（如 `HostInfo.ComputerName` 等）

## A4 ProxyItem 模型改造
- [ ] 1) 将 CreateTime 调整为 init-only（如目标要求）
- [ ] 2) 更新所有创建/反序列化代码，保证编译与运行
- [ ] 3) 确保 DataGrid/监控状态更新不受影响

## A5 文档同步（必须）
- [ ] 更新 `TODO.md`：Phase A 交付物已完成/未完成与证据块
- [ ] 更新 `docs/progress.md` / `docs/task-tracking.md` / `docs/changelog.md`：Phase A 的结构交付说明与验证结果

## 完成标准
- [ ] `dotnet build YLproxy.sln` 通过
- [ ] `dotnet test tests/YLproxy.Tests.csproj --filter TestCategory!=E2E` 通过
- [ ] Phase A A1~A4 对应文件结构与绑定路径满足目标描述
- [ ] 文档对 Phase A 的完成证据与代码一致

