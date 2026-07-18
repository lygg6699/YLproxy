# TODO

**更新时间：2026-07-19**

> **注意：** 本文件已迁移至 `docs/pending/TODO.md`，请参考新的文档结构。
> 当前项目使用 `docs/INDEX.md` 作为文档中心入口。

## Phase A 完成状态（✅ 已完成）

- [x] Phase A0：验收矩阵与文件清单锁定
- [x] Phase A1：DI 注册 + MainViewModel 构造链闭合（启动链可运行）（已复验 build/test）
- [x] Phase A2：接口抽取（按 Abstractions 目录文件）（关键编译闭环：AppSettingsService/GetConfig 与 IAppSettingsService 对齐；复验：build/test 全绿）
- [x] Phase A3：MainViewModel 拆分（HostInfo / Dashboard / LogPanel 子 ViewModel 组合模式）（build/test 复验通过：75/75）
- [x] Phase A4：ProxyItem 模型改造（CreateTime init-only）（build/test 复验通过：75/75）
- [x] Phase A5：文档同步（TODO/docs/progress/task-tracking/changelog 已更新 A3/A4 证据）

## Phase B 完成状态（🟡 部分完成）

- [x] Phase B1：代理认证与网络连接修复
- [x] Phase B2：DI 注册 + 接口对齐 + 启动链闭合
- [ ] Phase B3：数据持久化策略决策（待执行）
- [ ] Phase B4：MainViewModel 继续拆分（待执行）
- [x] Phase B5：Job Object 孤儿进程防护 + CI 加固
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

