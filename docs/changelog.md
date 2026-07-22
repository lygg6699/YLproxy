# 📝 版本变更历史

> 这是根级入口文件。详细变更历史见 [risks/changelog.md](risks/changelog.md)

**最后更新**：2026-07-22

## 2026-07-22
- chore: 项目全面清理与优化 — 提交 `cd69a44` 推送至 `origin/main`
  - 更新 ci.code-workspace 添加 VS Code 扩展推荐
  - 更新 global.json SDK 版本至 10.0.302
  - Phase 4 Git 仓库同步完成

### 补丁：同步修复与清理维护
- `4f50407` — chore: 完成Git同步和项目清理维护
  - 解决本地分支落后 remote 2个提交的问题
  - 解决 TODO.md 合并冲突
  - 清理 bin/、obj/、logs/ 和 3proxy 日志
  - 添加清理审计报告 docs/audit/cleanup-audit-20260722.md

### 步骤 2.1/2.2：安全加固 — pre-commit 钩子 + 日志轮转
- `[pending]` — 新增安全机制：
  - **pre-commit 钩子**：`.githooks/pre-commit` — 防止敏感文件（AppSettings.json、data/config.json、*.pem、*.key 等）被意外提交
  - **日志轮转脚本**：`scripts/cleanup-logs.ps1` — 清理 30天/100MB 以上应用日志、7天以上 3proxy 日志
  - **环境初始化脚本**：`scripts/init-environment.ps1` — 一键安装钩子 + 注册计划任务
  - 更新 `docs/deployment.md` 添加初始化步骤
  - 更新 `docs/development/00-快速开始.md` 添加钩子安装步骤

### 步骤 2.4：完善文档（2026-07-22）
- 全面完善项目文档，反映最新项目状态：
  - **README.md**（根目录）：添加「清理和维护指南」章节（日志轮转、Git 钩子、环境初始化、构建缓存清理），更新 AppSettings.json 配置说明（本地生成机制 + 安全保护）
  - **docs/development/README.md**：修复所有 `development-deployment-outline/` 失效链接；新增「开发环境设置指南」「Pre-commit 钩子配置说明」「日志管理说明」三个章节
  - **docs/deployment.md**：新增「部署前检查清单」「配置文件管理指南」「日志轮转配置说明」三个章节
