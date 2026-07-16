# YLproxy 部署清单

## 部署前检查

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | .NET SDK 10.0.301+ 已安装 | ☐ |
| 2 | .NET Runtime 10.0.x 已安装 | ☐ |
| 3 | Windows 10+ / Windows Server 2019+ | ☐ |
| 4 | 管理员权限可用 | ☐ |
| 5 | 防火墙允许 TCP 端口 9001-9100 | ☐ |
| 6 | `data/config.json` 已配置代理 | ☐ |
| 7 | 3proxy runtime 已就绪 (`runtime/3proxy/bin64/`) | ☐ |

## 构建发布包

```powershell
# 1. 构建 Release 发布包
.\build\publish.ps1 -Configuration Release -CreateZip

# 2. 验证输出
Get-ChildItem build\publish\

# 3. 检查关键文件
Test-Path build\publish\YLproxy.GUI.exe       # 必须存在
Test-Path build\publish\AppSettings.json      # 必须存在
Test-Path build\publish\runtime\3proxy\bin64\3proxy.exe  # 必须存在
```

## 安装 Windows 服务

```powershell
# 以管理员身份运行 PowerShell，然后执行：
.\scripts\install-service.ps1 -Install

# 验证服务状态
.\scripts\install-service.ps1 -Status
Get-Service -Name YLproxyService
```

## 首次运行数据迁移

应用启动后会自动触发 JSON → SQLite 迁移：
1. 自动创建 `data/config.json.migration.bak` 备份
2. 数据写入 `data/ylproxy.db`
3. 创建 `data/.migration_completed` 标记文件

### 回滚迁移（如需要）

```powershell
# 删除 SQLite 数据库和标记文件
Remove-Item data/ylproxy.db -ErrorAction SilentlyContinue
Remove-Item data/ylproxy.db-shm -ErrorAction SilentlyContinue
Remove-Item data/ylproxy.db-wal -ErrorAction SilentlyContinue
Remove-Item data/.migration_completed -ErrorAction SilentlyContinue

# 从备份恢复 JSON 配置
Copy-Item data/config.json.migration.bak data/config.json -Force
```

## 卸载

```powershell
# 以管理员身份运行 PowerShell
.\scripts\uninstall-service.ps1
# 或直接调用：
.\scripts\install-service.ps1 -Uninstall
```

**注意**: 卸载不会删除用户数据文件（`data/config.json`、`data/ylproxy.db`）

## 日志位置

| 日志 | 路径 |
|------|------|
| 应用日志 | `logs/log_YYYYMMDD.txt` |
| 3proxy 运行日志 | `runtime/3proxy/logs/3proxy-{Id}.log` |

## 防火墙规则

| 规则名 | 端口 | 方向 |
|--------|------|------|
| YLproxy Service Inbound | TCP 9001-9100 | 入站 |

## 故障排查

### 服务无法启动
```powershell
# 检查事件日志
Get-EventLog -LogName Application -Source "YLproxy" -Newest 20

# 手动运行以查看错误
.\src\YLproxy.GUI\bin\Release\net10.0-windows\YLproxy.GUI.exe
```

### 代理无法监听
```powershell
# 检查端口占用
netstat -ano | findstr "9001"

# 检查 3proxy.exe 是否存在
Test-Path runtime\3proxy\bin64\3proxy.exe
Test-Path runtime\3proxy\bin64\FilePlugin.dll
Test-Path runtime\3proxy\bin64\StringsPlugin.dll
```

### 数据库损坏
```powershell
# 重置数据库（从 JSON 重新迁移）
Remove-Item data/ylproxy.db data/ylproxy.db-shm data/ylproxy.db-wal data/.migration_completed
# 重启应用，将自动重新迁移
```
