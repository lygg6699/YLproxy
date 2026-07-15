# API 部署规范 - YLproxy

> YLproxy 统一 API 部署指南、配置管理、端点定义与运维说明

版本：V1.0  
最后更新：2026-07-15

---

## 📋 目录

1. [部署架构](#部署架构)
2. [系统要求](#系统要求)
3. [部署步骤](#部署步骤)
4. [配置管理](#配置管理)
5. [API 端点定义](#api-端点定义)
6. [监控与日志](#监控与日志)
7. [故障排查](#故障排查)
8. [常见问题](#常见问题)

---

## 部署架构

### 系统架构图

```
┌─────────────────────────────────────────────────────┐
│             用户客户端（WPF GUI）                     │
│  ├─ 代理列表管理                                      │
│  ├─ 代理启停控制                                      │
│  ├─ 实时监控面板                                      │
│  └─ 日志查看器                                        │
└──────────────────┬──────────────────────────────────┘
                   │
                   │ (配置读写)
                   ▼
┌─────────────────────────────────────────────────────┐
│             YLproxy 核心服务                         │
│  ├─ ProxyDataService (配置管理)                      │
│  ├─ ProxyTester (连通性测试)                         │
│  ├─ ProxyProcessManager (进程管理)                   │
│  ├─ PortManager (端口分配)                           │
│  ├─ MonitorService (状态监控)                        │
│  └─ ConfigGenerator (配置生成)                       │
└──────────────────┬──────────────────────────────────┘
                   │
                   │ (进程启动)
                   ▼
┌─────────────────────────────────────────────────────┐
│              3proxy 代理引擎                          │
│  ├─ SOCKS4/4a/5 服务                                │
│  ├─ HTTP/HTTPS 代理                                  │
│  ├─ FTP 代理                                         │
│  └─ 端口转发                                         │
└──────────────────┬──────────────────────────────────┘
                   │
                   │ (网络通信)
                   ▼
┌─────────────────────────────────────────────────────┐
│           外部网络 / 目标服务器                       │
└─────────────────────────────────────────────────────┘
```

### 部署拓扑

```
开发环境             生产环境
──────────           ────────

Win10/11             Win Server 2022
  │                      │
  ├─ YLproxy.sln         ├─ YLproxy-Release.exe
  │                      │
  ├─ 3proxy/ (local)     ├─ 3proxy/ (共享)
  │                      │
  └─ config.json         └─ config.json (备份管理)
```

---

## 系统要求

### 硬件要求

| 配置项 | 最小值 | 推荐值 | 说明 |
|---|---|---|---|
| **CPU** | 2核 | 4核+ | 代理转发密集型 |
| **内存** | 2 GB | 8 GB+ | 缓冲与监控 |
| **磁盘** | 500 MB | 1 GB+ | 日志与配置 |
| **网络** | 100 Mbps | 1 Gbps+ | 代理转发 |

### 软件要求

| 软件 | 版本 | 说明 |
|---|---|---|
| **操作系统** | Windows 10/11 或 Server 2019+ | 仅支持 Windows |
| **.NET Runtime** | 10.0+ | 核心运行环境 |
| **3proxy** | 最新版本 | 代理引擎（已集成） |
| **PowerShell** | 5.0+ | 部署脚本 |

### 网络要求

- ✅ 管理端口开放（本地）
- ✅ 代理端口可外部访问（配置中指定）
- ✅ 日志输出路径可写
- ✅ 配置文件路径可读写

---

## 部署步骤

### 第 1 步：准备部署包

```powershell
# 1. 编译发布版本
cd e:\GZQ\YLXCX\YLproxy
dotnet clean
dotnet restore
dotnet publish -c Release -o .\publish

# 2. 验证发布内容
Get-ChildItem .\publish | Format-Table

# 预期文件：
# - YLproxy.exe
# - YLproxy.dll
# - 依赖 .dll 文件
# - data/ (配置目录)
# - runtime/ (3proxy 引擎)
```

### 第 2 步：准备部署目录

```powershell
# 1. 在目标服务器创建部署目录
$deployPath = "C:\YLproxy"
New-Item -ItemType Directory -Path $deployPath -Force

# 2. 复制发布文件
Copy-Item ".\publish\*" -Destination $deployPath -Recurse -Force

# 3. 设置目录权限
icacls $deployPath /grant:r "NT AUTHORITY\NETWORK SERVICE:(OI)(CI)F"

# 4. 验证部署
Get-ChildItem $deployPath -Recurse | Measure-Object
```

### 第 3 步：配置文件初始化

```powershell
# 1. 检查配置文件
$configPath = "$deployPath\data\config.json"
if (-not (Test-Path $configPath)) {
    Write-Host "创建默认配置文件..."
    # 使用模板初始化
}

# 2. 验证配置文件格式
$config = Get-Content $configPath | ConvertFrom-Json
Write-Host "配置项数: $($config.proxies.Count)"

# 3. 检查日志目录
$logPath = "$deployPath\logs"
New-Item -ItemType Directory -Path $logPath -Force
```

### 第 4 步：测试部署

```powershell
# 1. 启动应用
cd $deployPath
.\YLproxy.exe

# 2. 验证启动（等待 5 秒）
Start-Sleep -Seconds 5

# 3. 检查日志
Get-Content ".\logs\latest.log" -Tail 20

# 4. 验证代理端口
netstat -ano | Select-String "LISTEN" | Select-String "127.0.0.1"
```

### 第 5 步：配置开机自启（可选）

```powershell
# 方式 A：Windows 任务计划程序
$trigger = New-ScheduledTaskTrigger -AtStartup
$action = New-ScheduledTaskAction -Execute "$deployPath\YLproxy.exe"
Register-ScheduledTask -TaskName "YLproxy" -Trigger $trigger -Action $action -RunLevel Highest

# 方式 B：Windows 服务（需要 ServiceHost 项目）
# sc.exe create YLproxy binPath= "C:\YLproxy\YLproxy.exe"
```

---

## 配置管理

### 配置文件位置

```
YLproxy/
├── AppSettings.json          # 全局应用配置
├── data/
│   └── config.json           # 代理列表配置
└── logs/
    ├── latest.log            # 最新日志
    └── archive/              # 日志归档
```

### AppSettings.json 结构

```json
{
  "App": {
    "Name": "YLproxy",
    "Version": "1.0.0",
    "RuntimePath": "./runtime/3proxy",
    "ConfigPath": "./data/config.json"
  },
  "Logging": {
    "Level": "Info",
    "MaxFileSize": "10MB",
    "RetentionDays": 30
  },
  "Security": {
    "EnableEncryption": true,
    "EncryptionKey": "your-secret-key"
  }
}
```

### data/config.json 结构

```json
{
  "version": "1.0",
  "proxies": [
    {
      "id": "proxy-001",
      "name": "代理1",
      "type": "SOCKS5",
      "protocol": "SOCKS5",
      "address": "192.168.1.100",
      "port": 9999,
      "username": "user",
      "password": "pass",
      "status": "active",
      "createdAt": "2026-07-15T10:00:00Z",
      "lastTested": "2026-07-15T15:30:00Z"
    }
  ],
  "settings": {
    "autoStart": true,
    "monitorInterval": 30,
    "logLevel": "info"
  }
}
```

### 配置变更步骤

```powershell
# 1. 备份当前配置
Copy-Item ".\data\config.json" ".\data\config.json.backup.$(Get-Date -f 'yyyyMMdd_HHmmss')"

# 2. 编辑配置（建议使用 UI）
# 手动编辑或通过 YLproxy 客户端修改

# 3. 验证 JSON 格式
$config = Get-Content ".\data\config.json" | ConvertFrom-Json
Write-Host "配置验证成功，代理数: $($config.proxies.Count)"

# 4. 重启应用以应用配置
# 关闭并重新启动 YLproxy.exe
```

---

## API 端点定义

> 注：当前版本为本地应用，暂无网络 API。以下为未来扩展的 REST API 规范预留。

### 代理管理端点

#### 获取代理列表
```
GET /api/proxies
响应：
{
  "code": 200,
  "data": [
    {
      "id": "proxy-001",
      "name": "代理1",
      "type": "SOCKS5",
      "address": "192.168.1.100",
      "port": 9999,
      "status": "active"
    }
  ],
  "timestamp": "2026-07-15T15:30:00Z"
}
```

#### 添加代理
```
POST /api/proxies
请求体：
{
  "name": "新代理",
  "type": "SOCKS5",
  "address": "192.168.1.101",
  "port": 10000,
  "username": "user",
  "password": "pass"
}
响应：
{
  "code": 201,
  "data": { "id": "proxy-002", "name": "新代理" },
  "message": "代理创建成功"
}
```

#### 测试代理
```
POST /api/proxies/{id}/test
请求体：
{
  "testUrl": "http://www.baidu.com",
  "timeout": 5000
}
响应：
{
  "code": 200,
  "data": {
    "connected": true,
    "responseTime": 234,
    "statusCode": 200
  }
}
```

#### 启动/停止代理
```
POST /api/proxies/{id}/start
POST /api/proxies/{id}/stop
响应：
{
  "code": 200,
  "data": { "id": "proxy-001", "status": "started" }
}
```

#### 删除代理
```
DELETE /api/proxies/{id}
响应：
{
  "code": 200,
  "message": "代理已删除"
}
```

### 监控端点

#### 获取实时状态
```
GET /api/monitor/status
响应：
{
  "code": 200,
  "data": {
    "totalProxies": 5,
    "activeProxies": 3,
    "cpuUsage": 12.5,
    "memoryUsage": 245.6,
    "networkActivity": {
      "inbound": 125.3,
      "outbound": 98.7
    }
  }
}
```

#### 获取日志
```
GET /api/logs?lines=100&level=info
响应：
{
  "code": 200,
  "data": [
    {
      "timestamp": "2026-07-15T15:30:45Z",
      "level": "INFO",
      "message": "代理启动成功"
    }
  ]
}
```

---

## 监控与日志

### 日志位置与轮转

```powershell
# 日志目录结构
logs/
├── latest.log              # 当前日志（动态）
├── archive/
│   ├── 2026-07-01.log      # 按日期归档
│   ├── 2026-07-02.log
│   └── ...
└── errors/                 # 错误日志专区
    ├── latest-errors.log
    └── ...

# 日志轮转策略
# - 日志文件大小: 10 MB
# - 保留天数: 30 天
# - 超出后自动压缩并归档
```

### 监控关键指标

```powershell
# 1. 代理状态监控脚本
$logPath = ".\logs\latest.log"
$activeProxies = (Get-Content $logPath | Select-String "proxy.*started" | Measure-Object).Count
Write-Host "活跃代理数: $activeProxies"

# 2. 资源使用监控
$proc = Get-Process YLproxy
Write-Host "CPU: $($proc.CPU)%"
Write-Host "内存: $($proc.WorkingSet / 1MB) MB"

# 3. 端口占用检查
netstat -ano | Select-String "LISTEN"
```

### 告警规则

| 事件 | 告警级别 | 响应 |
|---|---|---|
| 代理连接失败 | ⚠️ 警告 | 记录日志，自动重试 |
| 内存占用 > 500 MB | ⚠️ 警告 | 日志告警 |
| 端口被占用 | 🔴 错误 | 停止启动，返回错误 |
| 配置文件格式错误 | 🔴 错误 | 拒绝启动，输出错误信息 |

---

## 故障排查

### 常见问题

**Q1：启动失败，报错 "端口已被占用"**
```powershell
# 检查占用端口的进程
netstat -ano | Select-String ":9999"

# 获取进程详情
Get-Process -Id <PID>

# 方案：
# A. 修改配置中的端口号
# B. 关闭占用端口的进程
# C. 使用 Administrator 权限重启
```

**Q2：配置文件读取失败**
```powershell
# 检查文件权限
icacls .\data\config.json

# 检查 JSON 格式
$config = Get-Content .\data\config.json | ConvertFrom-Json -ErrorAction Stop

# 方案：
# A. 修复 JSON 语法
# B. 检查文件编码（应为 UTF-8）
# C. 重新生成配置文件
```

**Q3：代理连接测试失败**
```powershell
# 检查网络连接
Test-NetConnection -ComputerName 192.168.1.100 -Port 1080

# 检查代理服务是否正常
Get-Process 3proxy

# 方案：
# A. 检查代理服务器地址和端口
# B. 检查防火墙规则
# C. 查看 3proxy 日志
```

### 日志诊断步骤

```powershell
# 1. 获取最新错误
Get-Content ".\logs\latest.log" -Tail 50 | Select-String "ERROR|FAIL"

# 2. 时间段内的日志
$start = (Get-Date).AddHours(-1)
Get-Content ".\logs\latest.log" | Where-Object { $_ -match $start }

# 3. 特定代理的日志
Get-Content ".\logs\latest.log" | Select-String "proxy-001"

# 4. 导出诊断报告
Get-Content ".\logs\latest.log" | Out-File "diagnostic_$(Get-Date -f 'yyyyMMdd_HHmmss').txt"
```

---

## 常见问题

**Q：能否远程访问 YLproxy？**  
A：当前版本为本地应用，仅支持本机使用。未来可通过 REST API 实现远程控制。

**Q：如何备份代理配置？**  
A：定期备份 `data/config.json` 文件。建议自动化备份策略。

**Q：支持哪些代理协议？**  
A：SOCKS4/4a/5、HTTP/HTTPS、FTP（通过 3proxy 支持）。

**Q：如何升级版本？**  
A：参考本文档的 [部署步骤](#部署步骤) 重新部署新版本。

**Q：性能瓶颈在哪？**  
A：主要取决于 3proxy 引擎和网络环境。可通过增加硬件资源或优化配置改进。

---

**最后更新**：2026-07-15  
**维护者**：YLproxy 运维团队  
**文档版本**：V1.0
