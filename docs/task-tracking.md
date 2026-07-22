# 📋 待办任务管理

> 这是根级入口文件。详细任务清单见 [pending/task-tracking.md](pending/task-tracking.md)

**最后更新**：2026-07-22

## 已完成

### Phase 6: 安全加固 — pre-commit 钩子 + 日志轮转（2026-07-22）
- [x] 步骤2.1：建立 pre-commit 钩子
  - [x] 创建 `.githooks/pre-commit` 钩子模板（版本控制追踪）
  - [x] 创建 `scripts/init-environment.ps1` 安装脚本
  - [x] 检查敏感文件：AppSettings.json、data/config.json、*.pem、*.key 等
- [x] 步骤2.2：实施日志轮转策略
  - [x] 创建 `scripts/cleanup-logs.ps1` 日志清理脚本
  - [x] 清理策略：日志30天/100MB、3proxy日志7天
  - [x] 计划任务注册：每天 02:00 自动执行（可选）
- [x] 步骤2.3：文档同步 — deployment.md、00-快速开始.md、changelog.md、task-tracking.md
- [x] 步骤2.4：完善文档
  - [x] 更新 `README.md`（根目录）：添加清理和维护指南、更新配置说明
  - [x] 更新 `docs/development/README.md`：修复失效链接、添加开发环境设置指南、pre-commit 钩子配置说明、日志管理说明
  - [x] 更新 `docs/deployment.md`：添加部署前检查清单、配置文件管理指南、日志轮转配置说明

### Phase 5: Git同步与项目清理维护（2026-07-22）
- [x] 步骤1.1：Git同步与状态修复（fast-forward合并 + 解决冲突）
- [x] 步骤1.2：彻底清理构建缓存（bin/、obj/）
- [x] 步骤1.3：清理残留日志文件（logs/、3proxy/logs/）
- [x] 步骤1.4：处理本地配置文件（从暂存区移除 AppSettings.json）
- [x] 步骤1.5：提交并推送所有更改至远程仓库

