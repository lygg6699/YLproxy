# YLproxy 部署优化与参考项目对比分析报告

## 1. 参考项目部署逻辑深度分析

### 1.1 Nginx Proxy Manager (NPM)
- **部署方式**: 采用 Docker 容器化部署（多容器架构）。
- **技术栈**: 前端使用 React/Vue 构建，后端使用 Node.js (Express)，核心代理引擎为 Nginx。
- **配置管理**: 通过环境变量（如 `DB_MYSQL_HOST`, `DB_SQLITE_FILE`）和数据库进行动态配置管理。
- **数据持久化**: 挂载宿主机卷（如 `npm_data` 和 `le_data`）来持久化 Nginx 配置、SQLite 数据库和 Let's Encrypt 证书。
- **服务管理**: 依赖 Docker Compose 的 `restart: unless-stopped` 机制实现服务的自启动与守护。

### 1.2 Caddy Proxy Manager (CPM)
- **部署方式**: 采用 Docker 容器化部署，并引入了创新的旁路容器设计。
- **技术栈**: 前后端使用 Next.js (Bun/Node.js) 统一构建，核心代理引擎为 Caddy。
- **配置管理**: 深度结合 Caddy API 进行动态配置注入，无需频繁重启主进程。
- **数据持久化**: 使用 SQLite 存储管理数据，ClickHouse 存储分析日志，通过 Docker Volume 进行持久化。
- **服务管理**: 
  - 引入 `l4-port-manager` 旁路容器，通过监听配置目录，在 L4 代理端口发生变化时，自动通过 Docker Socket Proxy 重建 Caddy 容器以应用端口变更。

---

## 2. YLproxy 当前部署状态分析

### 2.1 当前部署方式
- **运行模式**: 目前主要作为 Windows 桌面 GUI 应用程序（WPF）或控制台程序手动运行。
- **配置管理**: 依赖本地的 `AppSettings.json` 和 `data/config.json`。
- **数据持久化**: 直接在本地磁盘的 `data/` 目录下读写 JSON 配置文件，凭据使用 Windows DPAPI 进行加密保护。
- **依赖管理**: 依赖本地的 .NET 10.0 运行时环境以及预先准备好的 3proxy 运行时二进制文件（`runtime/3proxy`）。

### 2.2 对比分析与不足
1. **平台局限性**: 当前深度绑定 Windows 环境（特别是 DPAPI 凭据加密和 WPF GUI），无法直接在 Linux/Docker 环境下运行。
2. **服务化缺失**: 缺乏 Windows 服务（Windows Service）或 Linux Daemon 支持，用户关闭 GUI 界面或注销登录后，代理服务可能会中断。
3. **依赖绑定**: 依赖本地安装的 .NET 运行时，部署不够开箱即用。

---

## 3. YLproxy 部署优化方案

为了提升 YLproxy 的部署灵活性、稳定性和跨平台能力，同时保持其完全独立性，制定以下部署优化方案：

### 3.1 Windows 服务化部署 (Windows Service)
- **方案**: 引入 `Microsoft.Extensions.Hosting.WindowsServices` 依赖，使 YLproxy.Core/Proxy 能够作为 Windows 服务后台运行。
- **优势**: 实现开机自启、后台静默运行，无需保持 GUI 窗口打开。

### 3.2 跨平台 Docker 容器化部署
- **方案**: 
  - 编写多阶段构建的 `Dockerfile`，将 .NET 10.0 运行时与 Linux 版 3proxy 编译打包进同一个轻量级镜像。
  - 引入跨平台的加密方案（如 AES-GCM + 环境变量密钥）作为 DPAPI 的备用方案，以便在 Linux 容器中安全存储凭据。
- **示例 `Dockerfile`**:
  ```dockerfile
  FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
  WORKDIR /src
  COPY . .
  RUN dotnet publish src/YLproxy.Core/YLproxy.Core.csproj -c Release -o /app

  FROM mcr.microsoft.com/dotnet/runtime:10.0
  WORKDIR /app
  COPY --from=build /app .
  # 安装 Linux 版 3proxy
  RUN apt-get update && apt-get install -y 3proxy
  ENTRYPOINT ["dotnet", "YLproxy.Core.dll"]
  ```

### 3.3 自动化安装与一键部署脚本
- **Windows 端**: 提供 `install-service.ps1` 脚本，一键注册 Windows 服务并配置防火墙端口。
- **Linux/Docker 端**: 提供 `docker-compose.yml` 模板，支持一键拉取并启动代理服务。
