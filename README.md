# YLproxy
> 零隧道 · 零污染 · 零干预 —— 本地代理转换管理程序

---

## 项目描述

YLproxy 是一款运行在 Windows 平台上的桌面 GUI 应用程序（基于 .NET / WPF），它的核心职能是：**将需要用户名密码认证的远程 HTTP 代理，转换为本地不需要认证的 HTTP 代理端口**，供模拟器、游戏客户端、浏览器扩展等软件使用。

典型场景：


模拟器/软件 → 127.0.0.1:9001（无需认证）
                  ↓
            YLproxy 本地代理转换
                  ↓
        第三方 HTTP 认证代理（需认证）
                  ↓
               目标网站


---

## 核心意图

**一句话定义：** 在网络环境受限于认证代理时，为无法配置认证的软件提供一条干净的代理出路。

**设计原则：**
- **零系统入侵** — 不修改系统路由、DNS、网络适配器或注册表
- **进程级隔离** — 只有主动设置使用本地端口的程序经过代理，系统其余流量不受影响
- **开箱即用** — 输入远程代理信息 → 测试 → 启动，三步完成

---

## 项目背景与目标

### 为什么存在

在许多企业、校园或受限网络环境中，访问外部网络必须通过一个需要用户名 + 密码认证的 HTTP 代理。然而大量软件（尤其是模拟器、老旧游戏、命令行工具、IoT 调试器）**不支持代理认证配置**，只支持填写 IP 和端口。这导致这些软件在上述网络中无法正常工作。

### 解决了什么问题

- 模拟器（如 MuMu、雷电、BlueStacks）无法配置认证代理 → 通过 YLproxy 分配本地端口即可使用
- 需要代理的网络环境，但不希望全局代理影响微信、浏览器等已有正常流量
- 多个软件需要走不同的代理出口时，为每个应用分配独立端口

### 预期成果

- 一个稳定的 Windows 桌面程序，安装后双击即用
- 支持多代理配置管理与独立启停
- 后台进程监控，代理异常自动感知
- 清晰的图形界面，零命令行操作
- 持久化采用本地 JSON（`data/config.json`，DPAPI 加密凭据）；经评估**不引入 SQLite**
- REST API 远程管理：代码已存在于 `src/YLproxy.Api`（Kestrel + Bearer Token + Swagger），但**默认未启用/未接入 GUI 启动链**，属预留能力

---

## 环境

| 项目     | 值                              |
| -------- | ------------------------------- |
| 操作系统 | Windows 10 / Windows 11（x64）  |
| 运行时   | .NET 10.0 SDK / Runtime（SDK 基线以 `global.json` 为准：10.0.200，rollForward=latestPatch） |
| 代理引擎 | 3proxy 0.9.7（由 `scripts/prepare-runtime.ps1` 准备） |
| 开发工具 | Visual Studio 2022+ / VS Code    |
| 版本     | 0.2.0                           |
| 最后更新 | 2026-07-19                      |

---

## 部署目录树及说明

YLproxy/
├── .agent                # 第一必读文件，规则与文件放置约束
├── AppSettings.json                 # 全局运行配置（根目录唯一配置入口）
├── global.json                       # .NET SDK 版本约束（10.0.200, rollForward=latestPatch）
├── Directory.Build.props             # 全局编译属性（Nullable/分析器/Release 门禁）
├── Directory.Packages.props          # 中央包版本管理
├── YLproxy.sln                       # 唯一解决方案入口
├── .github/                         # GitHub Copilot/Agent 配置
├── .vscode/                         # VS Code 调试、任务与编辑器配置
│
├── src/                             # —————— 源代码 ——————
│   ├── YLproxy.Models/              # 数据模型层：ProxyItem、ProxyStatus、代理数据模型
│   ├── YLproxy.Utils/               # 通用工具层：PathResolver 等路径/基础工具
│   ├── YLproxy.Core/                # 核心业务逻辑层
│   │   ├── Config/ProxyDataService.cs #   data/config.json 加载/保存（JSON 持久化主链）
│   │   ├── ProxyTester.cs           #   代理连通性测试（HTTP 请求 + 延迟测量）
│   │   └── MonitorService.cs        #   按 AppSettings 轮询监控代理进程状态
│   ├── YLproxy.Infrastructure/      # 全局配置与日志基础设施
│   ├── YLproxy.Proxy/               # 代理转发层
│   │   ├── ConfigGenerator.cs       #   动态生成 3proxy .cfg 配置文件
│   │   ├── ProxyProcessManager.cs   #   转发路径分流 + 3proxy 进程启停/状态检测
│   │   ├── ManagedProxyForwarder.cs #   .NET 本地转发器（处理认证上游 407/CONNECT）
│   │   └── ProxyRuntimeConfiguration.cs # 运行目录/DLL 配置桥接
│   ├── YLproxy.Api/                 # REST API（Kestrel + Bearer Token + Swagger，默认未启用）
│   └── YLproxy.GUI/                 # WPF 图形界面层（MVVM 架构）
│       ├── MainWindow.xaml          #   主窗口
│       ├── ViewModelBase.cs         #   MVVM ViewModel 基类（INotifyPropertyChanged）
│       ├── RelayCommand.cs          #   命令绑定实现
│       ├── MainViewModel.cs         #   主界面 ViewModel
│       ├── Views/MainView.xaml      #   主界面布局（信息区、操作区、代理列表、日志）
│       └── Views/AddProxyWindow.xaml #  添加代理弹窗（表单校验、端口分配）
│
├── runtime/                         # —————— 运行时依赖 ——————
│   └── 3proxy/                      # 3proxy 代理引擎及其运行数据
│       ├── bin64/                   #   本地准备的 3proxy.exe 与 DLL（不提交）
│       ├── cfg/                     #   生成的 {Proxy.Id}.cfg 文件（不提交）
│       └── logs/                    #   3proxy 自身日志（不提交）
│
├── data/                            # —————— 用户数据目录 ——————
│   ├── config.example.json          #   脱敏配置模板（提交）
│   └── config.json                  #   代理配置持久化 JSON（本地忽略）
│
├── logs/                            # —————— 日志目录（运行时生成） ——————
│
├── docs/                            # —————— 项目文档 ——————
│   ├── INDEX.md                     #   文档中心入口
│   ├── pending/                     #   待执行任务和计划
│   ├── incomplete/                  #   待完善领域和规划
│   ├── deployed/                    #   已完成部署和发布
│   ├── issues/                      #   已知问题和缺陷
│   ├── risks/                       #   风险评估和缓解措施
│   └── development/                 #   开发指南和参考文档
│
├── tests/                           # —————— 测试代码 ——————
│   ├── YLproxy.Tests.csproj         #   测试项目
│   ├── UnitTest1.cs                 #   单元测试
│   └── PathResolverTests.cs         #   路径解析测试
│
├── build/                           # —————— 构建脚本与产物（预留） ——————
│
└── .gitignore                       # Git 忽略规则

路径约定：开发运行和发布运行都以应用根目录为相对路径基准。`AppSettings.json` 只放在应用根目录；代理数据只放在 `data/config.json`；应用日志只放在 `logs/`；3proxy 生成配置和引擎日志分别放在 `runtime/3proxy/cfg/` 与 `runtime/3proxy/logs/`。禁止在 `src/` 下创建 `data/`、`logs/` 或 `runtime/` 作为运行时数据目录。


---

## 依赖与技术栈

| 类别       | 名称/库                        | 版本要求      | 用途                        |
| ---------- | ------------------------------ | ------------- | --------------------------- |
| 框架       | .NET SDK                       | ≥ 10.0        | 运行时与编译基础            |
| UI 框架    | WPF（Windows Presentation Foundation） | .NET 10.0 | 桌面图形界面                |
| 架构模式   | MVVM                           | —             | ViewModelBase / RelayCommand |
| 代理引擎   | 3proxy                         | 0.9.7（脚本下载） | 本地代理进程                |
| IDE        | Visual Studio 2022+ / VS Code  | —             | 开发与调试                  |
| 包管理器   | NuGet                          | —             | 依赖管理                    |

无需外部数据库、无需 Docker、无需 Web 服务器。

---

## 配置说明

### AppSettings.json（应用全局配置）

`AppSettings.json` 位于项目根目录，是应用全局配置的唯一入口：

```json
{
  "Logging": {
    "LogDirectory": "logs",
    "RetentionDays": 30,
    "MinLevel": "Info"
  },
  "Proxy": {
    "DataDirectory": "data",
    "ConfigFileName": "config.json",
    "PortRangeStart": 9001,
    "PortRangeEnd": 9100,
    "CheckIntervalSeconds": 5
  },
  "ThreeProxy": {
    "RuntimeDirectory": "runtime/3proxy",
    "RequiredDlls": ["FilePlugin.dll", "StringsPlugin.dll"]
  }
}
```

### data/config.json（代理数据持久化）

代理数据以对象形式保存，仓库只提供不含凭据的 `data/config.example.json` 模板：

```json
{
  "Proxies": [
    {
      "Id": 1,
      "Name": "示例代理",
      "RemoteHost": "proxy.example.com",
      "RemotePort": 8080,
      "Username": "dpapi:v1:<Windows 当前用户密文>",
      "Password": "dpapi:v1:<Windows 当前用户密文>",
      "LocalHost": "127.0.0.1",
      "LocalPort": 9001,
      "Status": "Stopped"
    }
  ]
}
```

`Username` 和 `Password` 由 Windows DPAPI 以当前用户作用域加密保存。旧明文配置迁移命令为：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\migrate-proxy-data.ps1 -Apply
```

密文不能直接复制到其他 Windows 用户或计算机；迁移前应使用脚本提供的回滚机制并重新录入凭据。

---

## 使用方式

### 快速上手

powershell
# 1. 克隆仓库
git clone https://github.com/lygg6699/YLproxy.git
cd YLproxy

# 2. 准备固定版本的 3proxy Windows x64 运行时
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\prepare-runtime.ps1

# 3. 编译
dotnet build YLproxy.sln

# 4. 直接启动 GUI
dotnet run --project src/YLproxy.GUI


或双击 `src/YLproxy.GUI/bin/Debug/net10.0-windows/YLproxy.GUI.exe` 直接运行。

首次启动会在 `data/config.json` 中创建本机配置。该文件包含代理凭据，已被 Git 忽略；仓库只提供
`data/config.example.json`，不要把真实配置强制加入提交。

### 界面操作流程


1. 点击「添加代理」→ 弹出窗口
2. 填写：代理名称、远程主机、端口、用户名、密码
3. 点击「确定」→ 自动分配本地端口（9001 起）
4. 在列表中选中该代理 → 点击「测试」
5. 测试通过后 → 点击「启动」
6. 在模拟器/目标软件中设置 HTTP 代理为 127.0.0.1:9001


### 命令行验证（无需 GUI）

powershell
# 使用 curl 验证本地代理是否工作（假设本地端口 9001）
curl -x http://127.0.0.1:9001 http://ip-api.com/json

# 或在 PowerShell 中测试
Invoke-WebRequest -Uri http://ip-api.com/json -Proxy http://127.0.0.1:9001


---

## 运行示例

### 典型场景：模拟器网络配置


模拟器（如 MuMu/雷电）WiFi 设置：
  ┌────────────────────────────┐
  │ 代理：手动                 │
  │ 主机名：127.0.0.1          │
  │ 端口：9001                 │
  │ 认证：无                   │
  └────────────────────────────┘
          ↓
  YLproxy 已启动代理「办公代理」(127.0.0.1:9001)
          ↓
  远程认证代理 → 目标网站


### API 风格调用（远期 / 预留）


# 启动代理
POST /api/proxy/{id}/start

# 停止代理
POST /api/proxy/{id}/stop

# 测试代理
POST /api/proxy/{id}/test

# 列出所有代理
GET  /api/proxy


---

## 网络隔离示意


┌─────────────────────────────────────────────────┐
│                   你的电脑                        │
│                                                   │
│   模拟器 ──→ 127.0.0.1:9001 ──→ 认证代理 ──→ 外网  │
│                                                   │
│   浏览器 ──→ 直连（不走代理，不受影响）            │
│                                                   │
│   微信   ──→ 直连（不走代理，不受影响）            │
│                                                   │
│   游戏   ──→ 直连（不走代理，不受影响）            │
└─────────────────────────────────────────────────┘


YLproxy **只影响** 那些显式配置了使用本地代理端口的进程。系统全局网络不受任何影响。

---

## 开发构建

powershell
# 完整解决方案编译
dotnet build YLproxy.sln

# 运行测试
dotnet test tests/YLproxy.Tests.csproj

# 发布为单文件
dotnet publish src/YLproxy.GUI -c Release -r win-x64 --self-contained true

### 终端乱码规避（Windows）

若终端出现异常中文乱码串，表示命令输出编码与终端解码不一致。  
本仓库已在 `.vscode/settings.json` 中固定 `DOTNET_CLI_UI_LANGUAGE=en-US`，建议在 VS Code 终端内执行 dotnet 命令。
