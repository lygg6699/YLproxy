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
