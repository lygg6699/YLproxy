# YLproxy 架构分析对比与优化方案报告

**报告日期：** 2026-07-16  
**分析范围：** Nginx Proxy Manager v2.15.1 / Caddy Proxy Manager v2.12.6 / YLproxy v0.2.0  
**分析目的：** 通过参考成熟开源代理管理项目的架构设计，为 YLproxy 制定架构优化方案

---

## 目录

1. [参考项目一：Nginx Proxy Manager 架构分析](#1-nginx-proxy-manager)
2. [参考项目二：Caddy Proxy Manager 架构分析](#2-caddy-proxy-manager)
3. [YLproxy 当前架构分析](#3-ylproxy-当前架构分析)
4. [三维对比分析](#4-三维对比分析)
5. [架构优化方案](#5-架构优化方案)
6. [CI/CD 与 DevOps 优化](#6-cicd-与-devops-优化)

---

## 1. Nginx Proxy Manager

### 1.1 项目概览

| 属性 | 值 |
|------|-----|
| 版本 | 2.15.1 |
| 许可证 | MIT |
| 定位 | Web 端 Nginx 反向代理 + SSL 证书管理面板 |
| 部署形态 | Docker 容器（单容器多进程） |
| 目标用户 | 服务器管理员、DevOps |

### 1.2 前端架构

**技术栈：**

| 技术 | 版本 | 用途 |
|------|------|------|
| React | 19.2.7 | UI 框架 |
| TypeScript | 6.0.3 | 类型系统 |
| Vite | 8.0.16 | 构建工具 |
| React Router DOM | 7.16.0 | 客户端路由 |
| TanStack React Query | 5.101.0 | 服务端状态管理 |
| Formik | 2.4.9 | 表单管理 |
| React Bootstrap + Tabler | 2.10.10 / 1.4.0 | UI 组件库 |
| React Intl | 10.1.11 | 国际化 (i18n) |
| Biome | 2.4.15 | Lint/格式化 |
| Vitest | 4.1.8 | 单元测试 |

**架构亮点：**
- **Provider 层级清晰**：RawIntlProvider → LocaleProvider → ThemeProvider → QueryClientProvider → AuthProvider → Router
- **路由守卫机制**：健康检查 → 初始化检查 → 认证检查 → 功能路由（4层门禁）
- **全路由懒加载**：所有页面组件使用 `React.lazy()` 按需加载
- **API 客户端层**：`api/backend/` 目录下 ~60 个独立 API 文件，每个端点一个模块
- **Token 栈式管理**：`AuthStore.ts` 支持多 token 堆栈（管理员模拟登录功能）
- **国际化完备**：react-intl + 多语言翻译文件

**目录结构：**
```
frontend/src/
├── api/backend/       # API 客户端层（按端点拆分）
├── components/        # 共享 UI 组件
│   ├── Form/          # 表单组件
│   └── Table/         # 表格组件
├── context/           # React Context 提供者
│   ├── AuthContext, LocaleContext, ThemeContext
├── hooks/             # 自定义 Hooks（按实体拆分）
├── locale/            # i18n 翻译文件
├── modals/            # 模态框组件
├── modules/           # 业务逻辑模块
│   ├── AuthStore.ts   # Token 存储
│   ├── Permissions.ts # 权限检查
│   └── Validations.tsx
├── notifications/     # 通知组件
├── pages/             # 页面组件（按路由拆分）
│   ├── Access/, AuditLog/, Certificates/
│   ├── Dashboard/, Login/, Nginx/, Settings/
│   ├── Setup/, Users/
```

### 1.3 后端架构

**技术栈：**

| 技术 | 用途 |
|------|------|
| Node.js + Express.js | HTTP 服务器 + REST API |
| Knex.js | 数据库迁移工具 |
| Objection.js | ORM（基于 Knex） |
| SQLite 3 | 默认数据库（可选 MySQL） |
| LiquidJS | Nginx 配置模板引擎 |
| AJV | JSON Schema API 请求校验 |
| OpenAPI 3.1.0 | API 文档规范 |
| Certbot | Let's Encrypt 证书自动化 |
| Nginx | 反向代理引擎 |

**后端分层架构：**
```
backend/
├── index.js              # 服务器入口
├── knexfile.js           # Knex 数据库配置
├── internal/             # 业务逻辑层（14个模块）
│   ├── 2fa.js            # 双因素认证
│   ├── access-list.js    # 访问列表管理
│   ├── audit-log.js      # 审计日志
│   ├── certificate.js    # SSL 证书管理（Let's Encrypt）
│   ├── nginx.js          # Nginx 配置生成和管理
│   ├── proxy-host.js     # 代理主机 CRUD
│   ├── redirection-host.js
│   ├── stream.js         # TCP/UDP 流转发
│   ├── token.js          # JWT 令牌管理
│   ├── user.js           # 用户管理 + 权限
│   ├── setting.js        # 全局设置
│   └── report.js         # 仪表盘报告
├── lib/                  # 共享库
│   ├── access/           # 权限 JSON Schema（每操作一个文件）
│   ├── access.js         # 权限引擎
│   ├── certbot.js        # Certbot 插件管理
│   ├── config.js         # 配置加载器
│   ├── error.js          # 自定义错误类
│   ├── express/          # Express 中间件（CORS, JWT, 分页, 日志）
│   └── validator/        # AJV 请求校验器
├── migrations/           # Knex 数据库迁移（19 个文件）
├── models/               # Objection.js 数据模型（15 个文件）
├── routes/               # Express 路由处理器
│   ├── nginx/            # Nginx 相关路由（6 个子路由）
│   ├── audit-log, reports, schema, settings
│   ├── tokens, users, version, ci
├── schema/               # OpenAPI 3.1.0 规范
│   ├── swagger.json      # 主规范（$ref 引用）
│   ├── components/       # Schema 组件
│   └── paths/            # 按端点拆分的路径定义
├── templates/            # LiquidJS Nginx 配置模板（19 个）
```

**启动流程（9 步）：**
1. `migrateUp()` — 数据库迁移
2. `setup()` — 创建默认管理员、默认设置、Certbot 插件、日志轮转
3. `getCompiledSchema()` — 编译 OpenAPI Schema
4. `internalIpRanges.fetch()` — 获取 IP 范围
5. `internalCertificate.initTimer()` — Let's Encrypt 每小时续期定时器
6. `internalIpRanges.initTimer()` — IP 范围定期更新
7. `app.listen(3000)` — 启动服务器
8. 失败重试机制（1 秒后重试）

**认证授权设计：**
- **JWT Bearer Token** 认证
- 三层中间件：`jwt.js`（提取） → `jwt-decode.js`（验证 + 创建 Access 对象）
- **细粒度权限**：每个 API 操作对应一个 JSON Schema 权限定义（`lib/access/` 目录）
- 密码哈希存储，2FA 支持

### 1.4 安全设计

- JWT Token 认证 + 过期刷新机制
- 密码 bcrypt 哈希存储
- 双因素认证 (2FA)
- 审计日志（audit-log）
- 权限细粒度控制（per-operation JSON Schema）
- CORS 中间件配置
- IP 访问控制列表
- Nginx 配置中存在 `_exploits.conf` 安全规则模板

### 1.5 CI/CD 与 DevOps

**Docker 部署：**
- 单容器多进程（Nginx + Node.js Express）
- 多架构支持（linux/amd64, linux/arm64, linux/arm/v7）
- 基于 Alpine Linux 轻量镜像

---

## 2. Caddy Proxy Manager

### 2.1 项目概览

| 属性 | 值 |
|------|-----|
| 版本 | 2.12.6 |
| 定位 | Web 端 Caddy 反向代理 + mTLS + WAF 管理面板 |
| 部署形态 | Docker Compose 多服务 |
| 目标用户 | 服务器管理员、安全工程师 |

### 2.2 前端架构

**技术栈：**

| 技术 | 版本 | 用途 |
|------|------|------|
| Next.js (App Router) | 16.2.10 | 全栈框架 |
| React | 19.2.7 | UI |
| TypeScript | 7.0.2 | 类型系统 |
| Tailwind CSS | 4.3.2 | 样式 |
| shadcn/ui | latest | UI 组件库（Radix UI 原语） |
| next-themes | 0.4.6 | 暗色/亮色主题 |
| ApexCharts | 5.16.0 | 图表 |
| MapLibre GL | 5.24.0 | 地图可视化 |
| sonner | 2.0.7 | Toast 通知 |
| cmdk | 1.1.1 | 命令面板 |

**架构亮点：**
- **Next.js App Router**：服务端组件 + 客户端组件混合渲染
- **Server Actions**：表单变更直接调用服务端逻辑，无需额外 API 层
- **无全局状态库**：依赖 Next.js Server Components 直接查询数据库；客户端仅使用 `useState`
- **ActionState 模式**：标准化的 `{ status, message }` 返回契约
- **shadcn/ui 组件**：30 个 UI 组件，基于 Radix UI 原语
- **CSP 安全头**：Next.js middleware 设置 per-request nonce

### 2.3 后端架构

**技术栈：**

| 技术 | 用途 |
|------|------|
| Next.js API Routes | REST API (/api/v1/*) |
| Drizzle ORM | 数据库 ORM |
| SQLite (better-sqlite3) | 主数据库（27 个迁移文件） |
| ClickHouse | 分析数据库（流量 + WAF 事件） |
| better-auth | 认证框架（OAuth2/OIDC + 凭证登录） |
| Caddy Admin API | 代理配置下发 |
| Coraza WAF | Web 应用防火墙 |
| Bun | JavaScript 运行时 + 包管理器 |

**数据库设计（30 张表）：**

| 表 | 用途 |
|-----|------|
| users, sessions, accounts, verifications | 认证系统 |
| oauth_providers, oauth_states | OAuth 配置 |
| proxy_hosts, l4_proxy_hosts | 代理主机配置 |
| certificates, ca_certificates, issued_client_certificates | 证书管理 |
| access_lists, access_list_entries | 访问控制 |
| forward_auth_* | 转发认证（5 张表） |
| groups, group_members | 用户组管理 |
| mtls_roles, mtls_role_certificates, mtls_access_rules | mTLS RBAC |
| audit_events, waf_blocked_events | 审计 + 安全 |
| api_tokens, settings, instances | 管理配置 |

**中间件安全（proxy.ts）：**
- 所有路由守卫（公开路由白名单）
- Per-request nonce-based CSP
- X-Frame-Options: DENY
- X-Content-Type-Options: nosniff
- Referrer-Policy, Permissions-Policy

**认证授权：**
- better-auth（支持凭证 + OAuth2/OIDC 多提供商）
- 基于角色的访问控制（admin/user）
- API Token 认证（独立于用户会话）
- 侵入防护：`enforceSafeUserDefaults()` 防止 OAuth 权限提升
- 登录速率限制：5 次/5 分钟窗口，15 分钟封禁

### 2.4 部署架构

**Docker Compose 多服务：**
```
services:
  web:           # Next.js Web 应用
  caddy:         # 自定义 Caddy 构建
  l4-port-manager: # L4 端口管理 sidecar
  clickhouse:    # 分析数据库
```

### 2.5 CI/CD

**5 个 GitHub Actions 工作流：**
1. `test.yml` — 每个 push/PR 运行 Vitest 测试
2. `docker-build-trusted.yml` — 构建并推送 3 个多架构 Docker 镜像到 GHCR
3. SBOM + provenance 证明生成
4. 标签策略：分支名、SemVer、SHA、latest

---

## 3. YLproxy 当前架构分析

### 3.1 项目概览

| 属性 | 值 |
|------|-----|
| 版本 | 0.2.0 (Phase 7) |
| 许可证 | MIT |
| 定位 | Windows 桌面本地代理认证转换工具 |
| 部署形态 | Windows 原生 WPF 桌面应用 |
| 目标用户 | 终端用户（模拟器/游戏玩家） |

### 3.2 技术栈

| 层级 | 技术 | 评价 |
|------|------|------|
| **运行时** | .NET 10.0 | ✅ 平台原生，性能好 |
| **UI** | WPF + MVVM | ✅ Windows 原生，但跨平台受限 |
| **语言** | C# 12 | ✅ 强类型、成熟生态 |
| **代理引擎** | 3proxy 0.9.7 | ✅ 轻量成熟 |
| **数据存储** | JSON 文件 | ⚠️ 缺少事务、并发、查询能力 |
| **凭据保护** | Windows DPAPI | ✅ 系统级安全 |
| **日志** | 自研 FileLogger | ⚠️ 功能基础 |
| **测试** | xUnit (8 个测试文件) | ⚠️ 覆盖率有限 |

### 3.3 模块划分

```
YLproxy.sln
├── YLproxy.GUI/              # WPF 图形界面（MVVM）
│   ├── MainWindow.xaml       # 主窗口
│   ├── MainViewModel.cs      # 核心 ViewModel（混合了太多职责）
│   ├── Views/MainView.xaml
│   ├── Views/AddProxyWindow.xaml
│   ├── ViewModels/AddProxyViewModel.cs
│   ├── ViewModelBase.cs      # INotifyPropertyChanged 基类
│   ├── RelayCommand.cs       # ICommand 实现
│   └── InverseBoolConverter.cs
├── YLproxy.Core/             # 核心业务逻辑
│   ├── ProxyTester.cs        # 代理连通性测试
│   ├── MonitorService.cs     # 后台进程监控
│   └── Config/               # 数据持久化
│       ├── ProxyDataService.cs
│       └── ProxyDataSerializer.cs
├── YLproxy.Models/           # 数据模型（零依赖）
│   ├── ProxyItem.cs
│   ├── ProxyStatus.cs
│   └── AppConfig.cs
├── YLproxy.Proxy/            # 3proxy 集成
│   ├── ProxyProcessManager.cs
│   ├── ConfigGenerator.cs
│   └── ProxyRuntimeConfiguration.cs
├── YLproxy.Infrastructure/   # 基础设施
│   ├── AppSettingsService.cs
│   ├── DpapiSecurityService.cs
│   ├── FileLogger.cs
│   ├── LoggerFactory.cs
│   ├── ExceptionHandler.cs
│   ├── ILogger.cs
│   └── ISecurityService.cs
├── YLproxy.Utils/            # 工具类
│   └── PathResolver.cs
└── tests/                    # 测试（8 个测试文件）
```

**依赖流向：** GUI → Core → {Infrastructure, Models, Utils, Proxy}. Models 和 Utils 为零依赖叶节点。✅ 分层清晰。

### 3.4 当前架构优点

1. **分层清晰**：6 个项目，依赖方向正确（GUI → Core → Infrastructure/Models/Utils/Proxy）
2. **零依赖叶节点**：Models 和 Utils 无项目依赖
3. **DPAPI 凭据保护**：Windows 系统级加密，`CurrentUser` 作用域
4. **原子写入**：`ProxyDataService` 使用 write-to-temp-then-move 策略
5. **MVVM 模式**：WPF 标准架构模式
6. **配置热加载**：`AppSettingsService` 使用 FileSystemWatcher
7. **日志按日滚动**：`FileLogger` 支持按日切分和保留天数清理

### 3.5 当前架构短板

| 问题 | 严重程度 | 详情 |
|------|---------|------|
| JSON 文件存储 | 中 | 无事务、并发冲突风险、无查询能力 |
| MainViewModel 职责过重 | 中 | 混合 UI 逻辑、业务逻辑、代理管理、日志处理 |
| 测试覆盖率低 | 中 | 8 个测试文件，集成测试被 CI 排除 |
| 无 API 层 | 低 | 无法远程管理（Phase P4 已规划） |
| 结构化审计缺失 | 低 | 仅文件日志，无可查询的操作审计记录 |
| 少数模块使用 Console.WriteLine | 低 | `ProxyProcessManager` 等模块仍使用控制台输出，未统一接入 `ILogger` |
| 空 catch 块风险 | 低 | 少量代码中存在空 catch 块，可能隐藏运行时异常 |
| 缺少单元/集成/E2E 测试分层 | 低 | 当前测试未按层级分类，CI 中集成测试被统一排除 |

#### MainViewModel 职责过重量化分析

`MainViewModel.cs` 当前承担以下职责（按方法统计）：

| 职责类别 | 方法数 | 包含操作 |
|---------|--------|---------|
| 代理 CRUD | ~4 | LoadProxies, AddProxy, DeleteProxy, SaveProxies |
| 代理操作 | ~4 | TestSelectedProxyAsync, StartSelectedProxy, StopSelectedProxy, RefreshProxyStatus |
| UI 状态管理 | ~6 | IsSelected, IsTesting, IsStarting, IsStopping, 状态锁管理 |
| 日志管理 | ~3 | Log, ClearLog, 日志格式化 |
| 监控集成 | ~2 | 初始化 MonitorService, RefreshDataGrid |
| 窗口协调 | ~3 | OpenAddProxyWindow, 窗口关闭回调 |

**拆分方向：** 将 MainViewModel 拆分为 `ProxyListViewModel`（列表管理）、`ProxyOperationViewModel`（操作+状态锁）、`LogViewModel`（日志显示+过滤）、`MainViewModel`（协调+初始化，目标 <150 行）。

### 3.6 数据流描述

#### 3.6.1 代理配置流（用户操作 → 3proxy 进程启动）

```
用户操作 GUI
  │
  ▼
MainViewModel.StartSelectedProxy()
  │
  ▼
ProxyDataService.Load() ─────────────────────────────────────────┐
  │  当前：读取 data/config.json → 反序列化 → 返回 List<ProxyItem>  │
  │  迁移后：读取 data/ylproxy.db → 解密凭据 → 返回 List<ProxyItem>  │
  ▼                                                               │
ConfigGenerator.Generate(ProxyItem)                               │
  │  生成 3proxy cfg 内容字符串（当前：字符串拼接；未来：Scriban 模板）│
  ▼                                                               │
ProxyProcessManager.Start(cfgContent, LocalPort)                  │
  │  1. 写入 runtime/3proxy/cfg/{Id}.cfg                          │
  │  2. 启动 runtime/3proxy/bin64/3proxy.exe --cfg={cfgPath}     │
  │  3. 返回 true（成功）/ false（失败）                            │
  ▼                                                               │
MainViewModel 更新 ProxyItem.Status → Running/Failed             │
  ▼                                                               │
UI 刷新（DataGrid 绑定更新）                                       │
```

#### 3.6.2 监控反馈流（后台监控 → UI 状态更新）

```
MonitorService 定时器（每 5 秒）
  │
  ▼
获取所有 Status == Running 的 ProxyItem 列表
  │
  ▼ (遍历每个 Running 代理)
ProxyProcessManager.IsRunning(id)
  │  检查 3proxy 进程是否存活（Process.HasExited）
  │
  ├── 存活 → 跳过，保持 Running
  │
  └── 已退出 → 
       ├── 更新 ProxyItem.Status → Failed
       ├── ILogger.LogWarning("3proxy process {id} exited unexpectedly")
       ├── 删除遗留的 runtime/3proxy/cfg/{Id}.cfg
       └── 触发 UI 刷新回调 → MainViewModel.RefreshDataGrid()
```

### 3.7 各层职责边界

#### 3.7.1 分层职责与依赖

| 项目 | 职责 | 依赖项目 | 对外公开接口 |
|------|------|---------|-------------|
| **YLproxy.Models** | 数据模型定义（DTO），零业务逻辑 | 无 | `ProxyItem`, `ProxyStatus`, `AppConfig` |
| **YLproxy.Utils** | 工具类，路径解析 | 无 | `PathResolver.ResolvePath()`, `GetRepositoryRoot()` |
| **YLproxy.Infrastructure** | 跨切面关注点：日志、安全、配置、异常处理 | Models, Utils | `ILogger`, `ISecurityService`, `FileLogger`, `DpapiSecurityService`, `AppSettingsService`, `ExceptionHandler` |
| **YLproxy.Core** | 核心业务逻辑：数据持久化、代理测试、后台监控 | Models, Utils, Infrastructure, Proxy | `ProxyDataService`, `ProxyDataSerializer`, `MonitorService`, `ProxyTester`, `DataMigrationService`（待建）, `SqliteProxyRepository`（待建） |
| **YLproxy.Proxy** | 3proxy 集成：配置生成、进程管理 | Models, Infrastructure | `ConfigGenerator`, `ProxyProcessManager`, `ProxyRuntimeConfiguration` |
| **YLproxy.GUI** | WPF MVVM 表示层：窗口、视图、ViewModel | Core, Models, Infrastructure | `MainWindow`, `MainView`, `AddProxyWindow`, `MainViewModel`, `AddProxyViewModel` |

#### 3.7.2 不应跨越的边界

| 边界规则 | 说明 |
|---------|------|
| **GUI 层不应直接访问文件系统** | 所有文件 I/O 通过 Core 层的 Service 完成；当前 GUI 通过 `ProxyDataService` 间接访问 JSON/SQLite |
| **Core 层不应引用 WPF 程序集** | Core 层不能使用 `System.Windows`、`System.Windows.Controls` 等 WPF 命名空间 |
| **Proxy 层不应直接操作用户配置** | 3proxy cfg 生成所需的数据由 Core 层传入，Proxy 层不自己读写 `data/config.json` 或 `data/ylproxy.db` |
| **Models 保持零依赖** | Models 项目不引用任何其他 YLproxy 项目或第三方 NuGet 包 |
| **Infrastructure 不引用 GUI 或 Proxy** | Infrastructure 是基础层，只能被上层依赖，不能反向依赖 |

### 3.8 架构设计定位：桌面应用 vs Web 面板

YLproxy 的 WPF 桌面应用架构与 Nginx Proxy Manager / Caddy Proxy Manager 的 Web 面板架构有本质差异：

| 维度 | YLproxy（桌面应用） | NPM / Caddy PM（Web 面板） |
|------|---------------------|---------------------------|
| **部署模式** | 本地 exe，双击运行，无需服务器 | Docker 容器，需要服务器/WSL/VM |
| **数据存储** | 本地 SQLite（嵌入进程内），零网络依赖 | 远程数据库（SQLite/MySQL/ClickHouse），通过网络连接 |
| **用户交互** | 原生 WPF 窗口，Windows 原生控件 | 浏览器，React/Next.js 前端 |
| **安全边界** | Windows DPAPI `CurrentUser` 隔离，凭据仅当前用户可解密 | Token/Session/JWT 认证，多用户权限体系 |
| **网络依赖** | 仅代理流量需要网络，配置管理完全离线可用 | 管理面板本身需要网络访问 |
| **多用户** | 不支持（设计意图），单用户本地工具 | 支持，内置用户管理+权限系统 |
| **远程管理** | 当前不支持，P2 阶段规划本地 REST API | 核心功能，内建 Web 管理面板 |

**结论：** YLproxy 采用 Windows 桌面应用架构是正确的技术选择。其目标用户场景（模拟器/游戏玩家在本地 Windows 机器上转换代理认证）决定了：不需要 Web UI、不需要 Docker、不需要多用户系统、不需要外部数据库服务。架构演进方向应该是增强桌面应用的质量和可维护性（SQLite 迁移、ViewModel 拆分、测试体系），而非向 Web 面板方向转型。健康检查 | 低 | 启动失败只能通过日志排查 |
| 日志文件无限增长风险 | 低 | 虽有保留天数但无大小限制 |
| 无版本化数据迁移 | 中 | 配置格式变更无迁移机制 |
| 无自动化 UI 测试 | 低 | 依赖手动验证 |
| 无发布打包自动化 | 中 | 发布靠手动 dotnet publish |
| 无监控/遥测 | 低 | 用户环境问题排查困难 |
| 3proxy 配置硬编码拼接 | 低 | 缺乏模板化，扩展性差 |

---

## 4. 对比分析

### 4.1 三维对比总览

| 维度 | NPM | Caddy PM | YLproxy | YL 差距 |
|------|-----|----------|---------|---------|
| **定位** | Web 面板 | Web 面板 | 桌面应用 | 不同赛道 |
| **前端** | React+Vite+TS | Next.js+TS | WPF+C# | 框架不同 |
| **后端** | Express.js | Next.js API | 无独立后端 | 桌面应用无需 |
| **数据库** | SQLite+MySQL | SQLite+ClickHouse | JSON 文件 | 需升级 SQLite |
| **ORM/迁移** | Knex+Objection | Drizzle ORM | 无 | 需引入 |
| **认证** | JWT+2FA | OAuth2+OIDC+API Token | Windows DPAPI | 需本地 API 认证 |
| **API 规范** | OpenAPI 3.1.0 | 内嵌文档 | 无 | Phase P4 规划 |
| **配置模板** | LiquidJS 19 个模板 | Caddy Admin API | 字符串拼接 | 可参考模板化 |
| **审计** | audit-log 表 | audit_events 表 | 仅文件日志 | 需结构化审计 |
| **监控** | 无内置 | ClickHouse 分析 | MonitorService 轮询 | 基础可用 |
| **CI/CD** | Docker 多架构 | 5 workflow, SBOM | 1 个 CI workflow | 可增强 |
| **测试** | Vitest+Cypress | Vitest+Playwright | xUnit (8文件) | 需增强覆盖 |
| **国际化** | react-intl | 无 | 中文 | 参考 NPM |
| **文档** | VitePress 站点 | README | docs/ 24 文件 | 较完善 |

### 4.2 关键差距分析

**数据层差距最大：** 两个参考项目都使用 SQLite + ORM + 版本化迁移，YLproxy 使用 JSON 文件。这是最优先需要改进的领域。

**API 层：** 两个参考项目都是 Web 服务，有完整的 REST API + 认证体系。YLproxy 作为桌面应用，本地 REST API 已在 TODO P4 阶段规划。

**测试差距：** NPM 有 Vitest + Cypress E2E，Caddy PM 有 Vitest + Playwright E2E。YLproxy 仅有 8 个 xUnit 测试文件，集成测试被 CI 排除。

**配置模板化：** NPM 用 LiquidJS 管理 19 个 Nginx 模板。YLproxy 的 3proxy 配置是字符串拼接，应该模板化。

### 4.3 定位差异（重要）

YLproxy 与两个参考项目有本质不同：
- **NPM/CPM** 是服务器端 Web 面板，多用户、Docker 部署、Web UI
- **YLproxy** 是 Windows 桌面应用，单用户、本地 exe、WPF UI

因此：
- ❌ 不需要 Docker 容器化
- ❌ 不需要 Web UI 框架（React/Next.js）
- ❌ 不需要多用户认证系统
- ✅ 需要 SQLite 替代 JSON（参考其迁移策略）
- ✅ 需要本地 REST API（Phase P4 已规划）
- ✅ 需要配置模板化（参考 NPM LiquidJS 方式）
- ✅ 需要增强 CI/CD 和测试

---

## 5. 优化方案

### 5.1 优先级矩阵

| 优先级 | 优化项 | 工作量 | 影响面 | 参考来源 |
|--------|--------|--------|--------|----------|
| 🔴 P0 | JSON → SQLite 数据迁移 | 大 | 全局 | NPM/Caddy |
| 🔴 P0 | 版本化数据迁移机制 | 中 | 数据层 | NPM Knex |
| 🟡 P1 | MainViewModel 职责拆分 | 中 | GUI 层 | NPM hooks 模式 |
| 🟡 P1 | 3proxy 配置模板化 | 小 | Proxy 层 | NPM LiquidJS |
| 🟡 P1 | 结构化审计日志 | 小 | Infrastructure | 双方共有的 audit 表 |
| 🟢 P2 | 测试覆盖率提升 | 大 | 全局 | Caddy 测试结构 |
| 🟢 P2 | 本地 REST API | 中 | 新模块 | NPM OpenAPI |
| 🟢 P2 | CI/CD 增强 | 中 | DevOps | Caddy 多 workflow |
| 🔵 P3 | 发布打包自动化 | 中 | DevOps | Caddy SBOM |
| 🔵 P3 | 应用健康检查端点 | 小 | Infrastructure | NPM /api 端点 |

### 5.2 P0：数据层升级（JSON → SQLite）

**新模块：YLproxy.Data/**

```
src/YLproxy.Data/
├── YLproxy.Data.csproj         # 引用 Microsoft.Data.Sqlite + Dapper
├── Database/
│   ├── DatabaseInitializer.cs   # 建库 + 迁移执行
│   └── MigrationRunner.cs       # 版本化迁移引擎
├── Migrations/
│   ├── Migration001_Initial.cs  # 初始 Schema
│   ├── Migration002_AuditLog.cs # 审计日志表
│   └── ...
├── Repositories/
│   ├── IProxyRepository.cs
│   ├── ProxyRepository.cs       # CRUD（Dapper）
│   ├── IAuditLogRepository.cs
│   └── AuditLogRepository.cs
└── Models/
    ├── ProxyEntity.cs           # DB 实体（区别于 Models 的 DTO）
    └── AuditLogEntity.cs
```

**迁移策略（参考 NPM Knex 方式）：**

1. 创建 `schema_version` 表记录当前迁移版本
2. 启动时自动执行未应用的迁移（`MigrationRunner.MigrateUp()`）
3. 每次迁移用事务包裹
4. 提供 CLI 回滚命令：`YLproxy migrate --rollback`
5. 保留 JSON 读取能力作为首次迁移的数据源（自动导入旧数据）
6. 迁移失败时保留旧 JSON 文件作为备份

**推荐 NuGet 包：**
- `Microsoft.Data.Sqlite` — 轻量、零系统依赖、Windows 原生支持
- `Dapper` — 微型 ORM，高性能，SQL 可控
- `FluentMigrator` — 可选的成熟迁移框架（或自研轻量版）

### 5.3 P1：MainViewModel 职责拆分

**当前问题：** `MainViewModel.cs` 包含代理 CRUD、测试、启停、日志、监控、UI 状态管理等，职责耦合严重。

**拆分方案（参考 NPM hooks 按实体拆分模式）：**

```
YLproxy.GUI/ViewModels/
├── MainViewModel.cs             # 主协调器（<150 行）
├── ProxyListViewModel.cs        # 代理列表管理（选中、刷新、排序）
├── ProxyOperationViewModel.cs   # 测试/启动/停止操作 + 状态锁管理
├── LogViewModel.cs              # 日志显示、过滤、级别切换、清空
└── StatusBarViewModel.cs        # 本机信息、运行时状态
```

**MainViewModel 瘦身后仅负责：**
- 初始化各子 ViewModel
- 订阅跨 ViewModel 事件
- 生命周期管理（启动/关闭）

### 5.4 P1：3proxy 配置模板化

**当前方式（字符串拼接）：**
```csharp
var sb = new StringBuilder();
sb.AppendLine($"parent 1000 {proxy.RemoteHost} {proxy.RemotePort}");
sb.AppendLine($"auth none");
// ... 更多拼接
```

**优化方案（参考 NPM LiquidJS 模板）：**

使用 **Scriban**（纯 C# 模板引擎，支持 Liquid 语法，零原生依赖）：

```
src/YLproxy.Proxy/Templates/
├── proxy_parent.conf.sbncs     # 认证代理模板
├── proxy_direct.conf.sbncs     # 非认证代理模板
└── proxy_defaults.conf.sbncs   # 公共配置片段
```

```csharp
var template = Template.Parse(File.ReadAllText("Templates/proxy_parent.conf.sbncs"));
var config = template.Render(new {
    remote_host = proxy.RemoteHost,
    remote_port = proxy.RemotePort,
    username = credential.Username,
    password = credential.Password
});
```

**优势：** 模板与逻辑分离、可单独测试、支持条件渲染、便于扩展

### 5.5 P1：结构化审计日志

参考两个项目的 `audit-log`/`audit_events` 表设计：

```sql
CREATE TABLE audit_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp TEXT NOT NULL DEFAULT (datetime('now')),
    action TEXT NOT NULL,          -- 'proxy_start', 'proxy_stop', 'test', 'config_change'
    target_type TEXT,              -- 'proxy', 'setting'
    target_id INTEGER,
    target_name TEXT,
    detail TEXT,                   -- JSON 格式附加信息
    result TEXT NOT NULL,          -- 'success', 'failure'
    error_message TEXT
);
```

记录关键操作：代理启动/停止/测试、配置变更、进程异常退出（MonitorService 检测到 Failed）

### 5.6 P2：测试覆盖率提升

**参考 Caddy PM 的三层测试结构：**

```
tests/
├── unit/                    # 单元测试（纯逻辑，无 I/O）
│   ├── ProxyDataServiceTests.cs
│   ├── ConfigGeneratorTests.cs
│   ├── ProxyTesterTests.cs (Mock HttpClient)
│   └── ...
├── integration/             # 集成测试（真实 SQLite/3proxy）
│   ├── ProxyRepositoryTests.cs
│   ├── MigrationRunnerTests.cs
│   ├── ProxyProcessManagerTests.cs
│   └── ...
└── e2e/                     # 端到端测试（GUI 自动化）
    ├── AddProxyE2ETests.cs  (WinAppDriver 或 FlaUI)
    └── ProxyLifecycleE2ETests.cs
```

**CI 中集成测试策略：**
- 单元测试：始终运行
- 集成测试：CI 中准备 3proxy 运行时后运行
- E2E 测试：仅在 release 分支运行（需要 Windows GUI 环境）

**推荐测试框架扩展：**
- `FlaUI` — WPF UI 自动化测试
- `NSubstitute` — Mock 框架
- `Coverlet` — 代码覆盖率（CI 已配置）

### 5.7 P2：本地 REST API

**参考 NPM OpenAPI 规范和 Caddy `/api/v1/` 结构：**

```
POST   /api/v1/proxies              # 创建代理
GET    /api/v1/proxies              # 列出代理
GET    /api/v1/proxies/{id}         # 获取代理详情
PUT    /api/v1/proxies/{id}         # 更新代理
DELETE /api/v1/proxies/{id}         # 删除代理
POST   /api/v1/proxies/{id}/test   # 测试代理
POST   /api/v1/proxies/{id}/start  # 启动代理
POST   /api/v1/proxies/{id}/stop   # 停止代理
GET    /api/v1/status               # 健康检查
GET    /api/v1/audit-log            # 审计日志
```

**技术选型：** ASP.NET Core Minimal API（Kestrel），默认绑定 `127.0.0.1`，可选 API Token 保护。

**安全策略（参考 Caddy CSP + 安全头）：**
- 默认绑定 loopback 地址
- Bearer Token 认证（本地生成随机 token 存储于 AppSettings.json）
- 请求频率限制
- 所有请求记录到审计日志

---

## 6. CI/CD & DevOps

### 6.1 当前 CI 分析

**现有 CI（`.github/workflows/ci.yml`）：**
- ✅ Windows runner
- ✅ .NET SDK from global.json
- ✅ 3proxy 运行时准备
- ✅ Debug + Release 构建（warnings-as-errors）
- ✅ 测试 + 覆盖率报告
- ❌ 无发布打包步骤
- ❌ 无分支策略（仅 main）
- ❌ 无版本号自动管理
- ❌ 无 SBOM 生成

### 6.2 优化方案

**增强 CI 工作流（参考 Caddy 多 workflow 策略）：**

```
.github/workflows/
├── ci.yml               # 每次 push/PR：构建 + 测试 + 覆盖率
├── release.yml           # tag 触发：构建 + 测试 + 发布打包 + 创建 Release
└── docs.yml              # docs/ 变更：部署文档站点（可选）
```

**release.yml 新增步骤：**
1. 从 tag 提取版本号
2. 更新 `AssemblyInfo.cs` 版本号
3. `dotnet publish` 生成自包含单文件 exe
4. 打包为 zip（含 exe + 3proxy 运行时 + AppSettings.json）
5. 生成 SBOM（`dotnet CycloneDX`）
6. 创建 GitHub Release 并上传 zip + SBOM
7. 可选：代码签名（如未来有证书）

### 6.3 版本管理优化

**SemVer 策略（参考 NPM .version 文件 + Caddy git tag）：**
- 根目录 `.version` 文件记录当前版本
- CI 从 `.version` 读取版本号
- 发布 tag 格式：`v{version}`
- CHANGELOG.md 自动生成（GitHub Release Notes）

### 6.4 发布打包

**当前：** `dotnet publish src/YLproxy.GUI -c Release -r win-x64 --self-contained true`

**优化后发布流程：**
```powershell
# 1. 准备 3proxy 运行时
./scripts/prepare-runtime.ps1

# 2. 发布应用
dotnet publish src/YLproxy.GUI -c Release -r win-x64 --self-contained true -o publish/YLproxy

# 3. 复制依赖
Copy-Item runtime/3proxy/bin64/* publish/YLproxy/runtime/3proxy/bin64/
Copy-Item AppSettings.json publish/YLproxy/
Copy-Item data/config.example.json publish/YLproxy/data/

# 4. 打包
Compress-Archive -Path publish/YLproxy/* -DestinationPath YLproxy-v0.2.0-win-x64.zip
```

### 6.5 文档治理

**当前：** `docs/` 下有 24 个文件，结构完整。`TODO.md` 作为执行清单。

**优化建议（参考 NPM VitePress 文档站点）：**
- 保持 `docs/` 目录结构
- 未来可考虑 GitHub Pages 自动部署文档站点
- 每个 Phase 完成后同步更新 `docs/progress.md`、`docs/changelog.md`

---

## 7. 总结

### 7.1 核心发现

1. **YLproxy 架构设计合理**：6 项目分层清晰，依赖方向正确，MVVM 模式规范。与参考项目相比，定位不同（桌面应用 vs Web 面板），因此很多参考项目的设计（Docker、Web UI、多用户认证）不适用于 YLproxy。

2. **最大差距在数据层**：JSON 文件存储是当前最大瓶颈，参考两个项目都使用 SQLite + ORM + 版本化迁移，这是 YLproxy 最急需改进的部分。

3. **配置模板化是可快速落地的优化**：参考 NPM 的 LiquidJS 模板系统，使用 Scriban 替代字符串拼接，工作量小、收益大。

4. **测试体系需要分层建设**：参考 Caddy PM 的 unit/integration/e2e 三层测试结构，逐步建立完整的测试金字塔。

5. **CI/CD 可增量增强**：当前 CI 已覆盖基本门禁，可按 P2-P3 优先级逐步加入发布打包、SBOM、版本管理。

### 7.2 执行路线图

```
P0 (立即):  SQLite 迁移 + 版本化迁移机制       → 数据可靠性
P1 (短期):  ViewModel 拆分 + 模板化 + 审计日志   → 代码质量和可维护性
P2 (中期):  测试体系 + 本地 REST API + CI 增强   → 质量和扩展性
P3 (长期):  发布自动化 + 健康检查 + 文档站点     → 运维成熟度
```

### 7.3 独立性与兼容性承诺

所有优化方案均基于 YLproxy 现有技术栈（.NET 10.0 / WPF / C# / 3proxy），不依赖 Nginx Proxy Manager 或 Caddy Proxy Manager 的任何代码或组件。参考项目的价值在于其架构模式和最佳实践——如 SQLite 迁移策略、模板引擎使用、测试金字塔结构、CI/CD 多工作流模式——而非直接引入其技术栈。

### 7.4 关键不采纳项（及原因）

| 参考项目特性 | 不采纳原因 |
|-------------|-----------|
| Docker 部署 | YLproxy 是 Windows 桌面应用 |
| React/Next.js 前端 | YLproxy 使用 WPF 原生 UI |
| OAuth2/OIDC 多用户认证 | 桌面单用户场景 |
| ClickHouse 分析数据库 | 过度设计，桌面应用无需 |
| Coraza WAF | 3proxy 场景不涉及 Web 防火墙 |
| Let's Encrypt 自动化 | YLproxy 是代理客户端，非服务端 |
| mTLS 证书管理 | 超出当前产品范围 |

---

## 附录 A：参考项目文件清单

### Nginx Proxy Manager 关键文件
- `/workspaces/nginx-proxy-manager/backend/index.js` — 服务器入口
- `/workspaces/nginx-proxy-manager/backend/knexfile.js` — 数据库配置
- `/workspaces/nginx-proxy-manager/backend/internal/` — 业务逻辑（14 模块）
- `/workspaces/nginx-proxy-manager/backend/lib/access.js` — 权限引擎
- `/workspaces/nginx-proxy-manager/backend/schema/swagger.json` — OpenAPI 规范
- `/workspaces/nginx-proxy-manager/backend/templates/` — Nginx 配置模板（19 个）
- `/workspaces/nginx-proxy-manager/frontend/src/App.tsx` — React 入口
- `/workspaces/nginx-proxy-manager/frontend/src/Router.tsx` — 路由守卫
- `/workspaces/nginx-proxy-manager/frontend/src/modules/AuthStore.ts` — Token 管理

### Caddy Proxy Manager 关键文件
- `/workspaces/caddy-proxy-manager/proxy.ts` — Next.js 中间件（CSP + 安全头）
- `/workspaces/caddy-proxy-manager/src/lib/db/schema.ts` — Drizzle Schema（30 张表）
- `/workspaces/caddy-proxy-manager/src/lib/auth-server.ts` — better-auth 配置
- `/workspaces/caddy-proxy-manager/src/lib/rate-limit.ts` — 登录速率限制
- `/workspaces/caddy-proxy-manager/src/lib/caddy.ts` — Caddy 配置生成（2981 行）
- `/workspaces/caddy-proxy-manager/src/lib/models/proxy-hosts.ts` — 代理主机模型（2163 行）
- `/workspaces/caddy-proxy-manager/docker/` — 多服务 Dockerfile
- `/workspaces/caddy-proxy-manager/.github/workflows/` — 5 个 CI/CD 工作流

---

## 附录 B：YLproxy 关键文件清单

| 文件 | 用途 |
|------|------|
| `YLproxy.sln` | 解决方案入口 |
| `global.json` | .NET SDK 10.0.301 约束 |
| `AppSettings.json` | 全局配置（唯一入口） |
| `Directory.Build.props` | MSBuild 全局属性 |
| `src/YLproxy.GUI/MainViewModel.cs` | 核心 ViewModel（待拆分） |
| `src/YLproxy.GUI/MainWindow.xaml` | 主窗口 |
| `src/YLproxy.Core/ProxyTester.cs` | 代理连通性测试 |
| `src/YLproxy.Core/MonitorService.cs` | 后台进程监控 |
| `src/YLproxy.Core/Config/ProxyDataService.cs` | JSON 数据持久化（待迁移） |
| `src/YLproxy.Proxy/ProxyProcessManager.cs` | 3proxy 进程管理 |
| `src/YLproxy.Proxy/ConfigGenerator.cs` | 3proxy 配置生成（待模板化） |
| `src/YLproxy.Infrastructure/DpapiSecurityService.cs` | DPAPI 凭据加密 |
| `src/YLproxy.Infrastructure/FileLogger.cs` | 文件日志 |
| `src/YLproxy.Infrastructure/AppSettingsService.cs` | 配置热加载 |
| `.github/workflows/ci.yml` | CI 质量门禁 |
| `docs/` | 24 个文档文件 |

---

*报告完成。参考项目已删除，YLproxy 保持完全独立。*