# 🚀 部署变更记录

> 这是根级入口文件。详细部署记录见 [deployed/deployment.md](deployed/deployment.md)

**最后更新**：2026-07-22

---

## 📋 部署前检查清单

在部署或更新 YLproxy 之前，请逐一检查以下项目：

### 环境检查

| # | 检查项 | 验证命令 | 状态 |
|---|--------|---------|------|
| 1 | .NET SDK ≥ 10.0.301 已安装 | `dotnet --version` | ☐ |
| 2 | .NET Runtime 10.0.x 已安装 | `dotnet --list-runtimes` | ☐ |
| 3 | Windows 10+ / Windows Server 2019+ | `[Environment]::OSVersion` | ☐ |
| 4 | 管理员权限可用（安装服务/计划任务需要） | `whoami` | ☐ |
| 5 | 防火墙允许 TCP 9001-9100 | `netsh advfirewall show rule` | ☐ |

### 运行时检查

| # | 检查项 | 验证命令 | 状态 |
|---|--------|---------|------|
| 6 | 3proxy 运行时已就绪 | `Test-Path "runtime/3proxy/bin64/3proxy.exe"` | ☐ |
| 7 | 代理数据配置已就绪 | `Test-Path "data/config.json"` | ☐ |
| 8 | 全局配置已创建 | `Test-Path "AppSettings.json"` | ☐ |

### 安全与维护检查

| # | 检查项 | 说明 | 状态 |
|---|--------|------|------|
| 9 | Git pre-commit 钩子已安装 | `scripts/init-environment.ps1 -InstallHooks` | ☐ |
| 10 | 日志清理计划任务已注册 | `scripts/init-environment.ps1 -InstallScheduledTask`（可选） | ☐ |
| 11 | 验证无敏感文件在暂存区 | `git diff --cached --name-only` | ☐ |

> 详细部署步骤参见 [build/deploy-checklist.md](build/deploy-checklist.md)

---

## ⚙️ 配置文件管理指南

### 配置文件一览

| 文件 | 位置 | 生成方式 | Git 追踪 | 包含敏感信息 |
|------|------|---------|---------|-------------|
| AppSettings.json | 项目根目录 | 应用首次启动自动创建 | ❌ 已忽略 | ⚠️ 路径等本地信息 |
| AppSettings.example.json | 项目根目录 | 手动维护 | ✅ 已追踪 | ❌ 无 |
| data/config.json | `data/` | 运行时生成 | ❌ 已忽略 | ⚠️ DPAPI 加密凭据 |
| data/config.example.json | `data/` | 手动维护 | ✅ 已追踪 | ❌ 无 |

### AppSettings.json

应用全局配置的唯一入口，初始化方式：

```powershell
# 方式一：应用首次启动自动生成（推荐）
dotnet run --project src/YLproxy.GUI

# 方式二：从模板复制
Copy-Item AppSettings.example.json AppSettings.json

# 方式三：使用环境初始化脚本
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\init-environment.ps1
```

> **安全提醒**：AppSettings.json 已被 `.gitignore` 和 pre-commit 钩子双重保护，
> 禁止强制提交到仓库（`git add -f AppSettings.json`）。

### data/config.json

代理数据持久化文件（含 DPAPI 加密凭据）：

- 首次运行 GUI 或 API 时自动创建
- 凭据使用 Windows DPAPI 以当前用户作用域加密
- 仓库仅提供脱敏模板 `data/config.example.json`

#### 凭据迁移

```powershell
# 将旧明文凭据迁移为 DPAPI 加密
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\migrate-proxy-data.ps1 -Apply
```

> 密文与当前 Windows 用户绑定，无法跨用户或跨机器迁移。

### 配置验证

```powershell
# 验证 JSON 格式正确性
Get-Content "AppSettings.json" | ConvertFrom-Json | Out-Null
Get-Content "data/config.json" | ConvertFrom-Json | Out-Null
```

---

## 🔄 日志轮转配置说明

### 日志存储位置

| 日志类型 | 默认路径 | 轮转方式 |
|---------|---------|---------|
| 应用日志 | `logs/log_YYYYMMDD.txt` | 按大小 + 时间轮转 |
| 3proxy 引擎日志 | `runtime/3proxy/logs/` | 按时间轮转 |

### 轮转策略

由 `scripts/cleanup-logs.ps1` 脚本统一管理：

```powershell
# 手动执行清理
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\cleanup-logs.ps1

# 预览模式（不实际删除）
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\cleanup-logs.ps1 -WhatIf

# 自定义保留策略
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\cleanup-logs.ps1 -MaxAgeDays 60 -MaxSizeMB 200 -ProxyLogMaxAgeDays 14
```

| 策略 | 默认值 | 可配置参数 | 配置来源 |
|------|--------|-----------|---------|
| 应用日志保留天数 | 30 天 | `-MaxAgeDays` | AppSettings.json → Logging.RetentionDays |
| 应用日志大小上限 | 100 MB | `-MaxSizeMB` | cleanup-logs.ps1 参数 |
| 3proxy 日志保留天数 | 7 天 | `-ProxyLogMaxAgeDays` | cleanup-logs.ps1 参数 |

### 自动定时清理

```powershell
# 注册每日 02:00 自动清理（需要管理员权限）
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\init-environment.ps1 -InstallScheduledTask

# 卸载计划任务
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\init-environment.ps1 -UninstallScheduledTask
```

### 日志级别配置

通过 `AppSettings.json` 调整日志级别：

```json
{
  "Logging": {
    "MinLevel": "Info"   // Debug, Info, Warn, Error
  }
}
```

> **建议**：开发环境使用 `Debug`，生产环境使用 `Warn` 以减少磁盘占用。

---

## 2026-07-22
- `4f50407` — chore: 完成Git同步和项目清理维护
  - 同步远程仓库更改（fast-forward合并 `43bb28d` + `cd69a44`）
  - 清理构建缓存、日志文件和本地配置
  - 更新API部署规范和测试代码
  - 添加清理审计报告 `docs/audit/cleanup-audit-20260722.md`
- `[pending]` — 新增安全加固机制：
  - 创建 `.githooks/pre-commit` 钩子模板（阻止敏感文件提交）
  - 创建 `scripts/cleanup-logs.ps1` 日志轮转脚本
  - 创建 `scripts/init-environment.ps1` 一键环境初始化
  - 详情见 `deployed/deployment.md` 部署清单更新
