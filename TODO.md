# TODO

## DevContainer 修复（Phase 1）
- [x] 1) 在 `.devcontainer/devcontainer.json` 的 `customizations.vscode.extensions` 中追加 `ms-vscode.test-adapter-converter`
- [x] 2) 在 `.devcontainer/post-create.sh` 中移除 `dotnet build` 的 `--no-restore` 参数，避免还原失败导致构建直接失败
- [x] 3) 端口转发与 ASP.NET 证书配置项：已确认 forwardPorts=[9100,9001]；DOTNET_GENERATE_ASPNET_CERTIFICATE=false

## 安全性优化（Phase 2）
- [x] remoteUser 已为 `vscode`（已核查：无需在 Codespaces 中以 root 运行）


## Phase A 完成状态（✅ 已完成）

- [x] Phase A0：验收矩阵与文件清单锁定
- [x] Phase A1：DI 注册 + MainViewModel 构造链闭合（启动链可运行）（已复验 build/test）
- [x] Phase A2：接口抽取（按 Abstractions 目录文件）（关键编译闭环：AppSettingsService/GetConfig 与 IAppSettingsService 对齐；复验：build/test 全绿）
- [~] Phase A3：MainViewModel 拆分 —— **部分完成**：仅抽出 HostInfo / Dashboard / LogPanel 三个纯展示子 ViewModel；核心代理业务（命令/启停/批量/导入导出/监控接线）仍集中在约 832 行的 MainViewModel，协调器瘦身在 B4 继续
- [x] Phase A4：ProxyItem 模型改造（CreateTime init-only）（build/test 复验通过：75/75）
- [x] Phase A5：文档同步（TODO/docs/progress/task-tracking/changelog 已更新 A3/A4 证据）

## Phase B 完成状态（🟡 部分完成）

- [x] Phase B1：代理认证与网络连接修复
- [x] Phase B2：DI 注册 + 接口对齐 + 启动链闭合
- [x] Phase B3：数据持久化策略决策 —— **已决策（方案 A：JSON-only）**，已删除未接线的 SQLite 层（SqliteProxyRepository / DataMigrationService / IProxyRepository 及相关依赖）
- [ ] Phase B4：MainViewModel 继续拆分（待执行，承接 A3 未完成部分）
- [~] Phase B5：**CI 加固已完成**；**Job Object 孤儿进程防护尚未实现**（代码中无 CreateJobObject/AssignProcessToJobObject；父进程崩溃时 3proxy 子进程可能成为孤儿）——已拆出独立后续任务
- [ ] Phase B5-new：实现 Job Object 孤儿进程防护（待执行）
- [ ] Phase B6：代码清理与优化（待执行）

## Phase C 待执行（🔴 待启动）

- [ ] Phase C1：P0 阻塞性债务清偿
- [ ] Phase C2：P1 重要性债务清偿
- [ ] Phase C3：P2 优化性债务清偿

---

**详细执行计划请参考：**
- `docs/pending/debt-remediation-execution-plan-20260719.md`
- `docs/pending/task-tracking.md`
- `docs/incomplete/01-开发路线图.md`

