# TODO：Phase A（DI + 拆分 + 接口抽取）与文档一致性闭环修复

**更新时间：2026-07-18**

## 策略
- 采用：A 分阶段迁移（每完成一步编译+测试+同步文档状态，避免再次出现“代码未落地但文档宣称已完成”）

---

## Phase A0：验收矩阵与文件清单锁定
- [ ] 建立 Phase A A1~A4 验收矩阵（按你的目标逐项列出）
- [ ] 参考执行步骤：`TODO_PHASEA_WORKFLOW.md`

- [ ] 列出需要新增/编辑的源文件与文档文件清单
- [ ] 以现状代码做差距确认（记录到 TODO）

---

## Phase A1：补齐 DI 注册清单与启动链闭合
- [ ] 更新 `Directory.Packages.props`：确保 `Microsoft.Extensions.DependencyInjection 9.0.0`
- [ ] 重构 `src/YLproxy.GUI/App.xaml.cs`：补齐注册清单（IAppSettingsService/ProxyConfig/ThreeProxyConfig/ApiConfig/ISecurityService等）
- [ ] 重构 `src/YLproxy.GUI/MainWindow.xaml.cs`：确保窗口 DataContext 由 DI 解析（MainViewModel 由 provider 创建）
- [ ] 更新 `src/YLproxy.GUI/ServiceLocator.cs`：确保 provider 生命周期正确
- [ ] 编译：`dotnet build YLproxy.sln`
- [ ] 测试：`dotnet test tests/YLproxy.Tests.csproj`
- [ ] 同步 `docs/progress.md`、`docs/changelog.md`、`docs/task-tracking.md`：标注 A1 完成/待完成

---

## Phase A2：接口抽取（A2 接口文件 + 现有实现落地 + 注入替换）
- [ ] 新增接口文件：
  - `src/YLproxy.Core/Abstractions/IProxyDataService.cs`
  - `src/YLproxy.Core/Abstractions/IProxyTester.cs`
  - `src/YLproxy.Core/Abstractions/IProxyRepository.cs`
  - `src/YLproxy.Proxy/Abstractions/IProxyProcessManager.cs`
  - `src/YLproxy.Infrastructure/Abstractions/IAppSettingsService.cs`
- [ ] 让现有类实现接口（ProxyTester/ProxyProcessManager/ProxyDataService/AppSettingsService等）
- [ ] 替换调用点：MainViewModel/GUI 通过构造注入接口而不是 `new` 具体实现
- [ ] 编译 + 测试
- [ ] 同步文档状态（A2 完成标注）

---

## Phase A3：MainViewModel 拆分（协调器 + 4 子 VM）
- [ ] 新增子 VM 文件：HostInfoViewModel/DashboardViewModel/LogPanelViewModel/ProxyOperationViewModel（或你指定名称）
- [ ] 改造 `src/YLproxy.GUI/MainViewModel.cs`：变为协调器（仅组合、少量跨 VM 协调）
- [ ] 改造 `src/YLproxy.GUI/Views/MainView.xaml`：绑定从 `{Binding XXX}` 改为 `{Binding HostInfo.XXX}` 等子路径
- [ ] 保持按钮行为与状态锁逻辑一致（防止回归“第二次点击失效”）
- [ ] 编译 + 测试
- [ ] 同步文档状态（A3 完成标注）

---

## Phase A4：ProxyItem 模型按目标精化（init-only 约束落地并验证反序列化）
- [ ] 按目标修正 `src/YLproxy.Models/ProxyItem.cs`：CreateTime/Id init-only（其它 get/set）
- [ ] 检查 JSON 序列化/反序列化与赋值路径（ProxyDataService/迁移脚本/测试）
- [ ] 编译 + 测试
- [ ] 同步文档状态（A4 完成标注）

---

## Phase B0：文档与代码一致性闭环修复
- [ ] 审计 `docs/progress.md` / `docs/changelog.md` / `docs/task-tracking.md`：删除或修正“Phase A 已完成”但代码未落地的宣称
- [ ] 为每个 A1~A4 子项补充“完成证据”：列出关键文件路径/功能验证点
- [ ] 统一文档版本号与更新时间（v0.3.0 目标语义）
- [ ] 编译 + 测试（最终确认）

---

## Phase J：收尾
- [ ] 清理遗留 Console 输出/空 catch（如存在且与当前 Phase 无冲突）
- [ ] 确认 CI 通过（如必要可补充过滤器）

