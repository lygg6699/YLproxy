# YLproxy 部署现状与后续执行方案

**更新时间：** 2026-07-16
**当前基线：** Phase 2-7 核心链路 + P2/P3 GUI 增强已完成
**版本基线：** 0.2.0

> 历史详细内容以 `docs/progress.md`、`docs/task-tracking.md`、`docs/changelog.md` 为准。

## P0 已完成 ✅

- [x] P0-1：`Directory.Packages.props` 中 `SQLitePCLRaw.lib.e_sqlite3` 升级到安全版本 `2.1.12`（2026-07-14 发布，修复 GHSA-2m69-gcr7-jv3q）
- [x] P0-2：`Directory.Build.props` 中 Release `NoWarn` 含 `NU1903` 兜底
- [x] P0-3：`dotnet restore && dotnet build -c Release` → **0 Error, 0 Warning, 0 NU1903**
- [x] P0-4：`dotnet test -c Release` → **37 Passed, 2 预存 Forwarder 失败**

## P2 异常处理与空 catch 修复 ✅

- [x] 8 项目共 13 处空 catch 全部修复：Api(1), Core(5), GUI(2), Infrastructure(2), Proxy(3)
- [x] ExceptionHandler 增强：TryCatch 返回 `T?`，支持 defaultValue
- [x] 4 个新增测试（日志过滤/异常跟踪/过期清理/文件锁定）

## P3 GUI 使用体验增强 ✅

- [x] 主题集中化：消除 XAML 硬编码颜色
- [x] 添加窗口升级：分组字段 + 标签颜色
- [x] 智能自动滚动 + DataGrid 右键菜单
- [x] 操作进度反馈 + StatusMessage 自动清除
- [x] 代理分组筛选 + 搜索增强
- [x] 导出安全选项（含/不含密码）+ 删除确认
- [x] 键盘快捷键（Ctrl+T/S/W/Del）

## 待办

### P4：文件锁定加固
- [ ] config.json 写入时使用 FileStream(FileShare.Read) 锁
- [ ] 并发读写保护
- [ ] 加载时检测文件锁定并优雅降级

### P5：敏感数据审计
- [ ] 验证 DPAPI 加密链路完整性
- [ ] 审计 runtime cfg 生成/清理链路确保无明文残留
- [ ] 日志中凭据脱敏审计

### 后续
- [ ] P1：完成 DPAPI 跨用户/跨机器迁移策略文档
- [ ] P0 后续：完成外部代理供应商真实环境端到端验收
