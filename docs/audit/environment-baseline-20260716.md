# YLproxy 环境基线审计报告
## 审计日期: 2026年7月16日
## 执行人: 执行师1

---

## 任务 1: 运行环境审计

### 检查项 1: .NET SDK
- **状态**: ✅ 通过
- **命令**: `dotnet --list-sdks`
- **结果**: `10.0.301 [C:\Program Files\dotnet\sdk]`
- **期望**: `>= 10.0.301`
- **评估**: 与 `global.json` 中指定的版本完全一致

### 检查项 2: .NET Runtime
- **状态**: ✅ 通过
- **命令**: `dotnet --list-runtimes`
- **结果**:
  - `Microsoft.AspNetCore.App 10.0.9`
  - `Microsoft.NETCore.App 10.0.9`
  - `Microsoft.WindowsDesktop.App 10.0.9`
- **期望**: `>= Microsoft.NETCore.App 10.0.x`
- **评估**: 运行时版本 10.0.9，高于 SDK 10.0.301 对应的运行时基线，正常

### 检查项 3: 3proxy 运行时
- **状态**: ✅ 通过
- **路径**: `runtime/3proxy/bin64/3proxy.exe`
- **文件存在**: 是
- **版本**: `3proxy-0.9.7` (via FileVersionInfo)

### 检查项 4: 3proxy 必要 DLL
- **状态**: ✅ 通过
- **FilePlugin.dll**: 存在 (`runtime/3proxy/bin64/FilePlugin.dll`)
- **StringsPlugin.dll**: 存在 (`runtime/3proxy/bin64/StringsPlugin.dll`)

### 检查项 5: 现有代理数据
- **状态**: ✅ 通过
- **路径**: `data/config.json`
- **代理数量**: 4
- **代理详情**:

| Id | Name    | Status (0=Stopped) | LocalPort |
|----|---------|---------------------|-----------|
| 1  | Proxy-1 | Stopped             | 9001      |
| 2  | Proxy-2 | Stopped             | 9002      |
| 3  | Proxy-3 | Stopped             | 9003      |
| 4  | Proxy-4 | Stopped             | 9004      |

- **评估**: 全部为 Stopped 状态（正常，未在运行中测试）

### 检查项 6: Windows Service 基础设施
- **状态**: ✅ 通过 (sc.exe 可用) / ⚠️ 管理员权限不足
- **sc.exe query**: 正常执行，返回系统服务列表
- **当前用户**: `desktop-3d19ugq\y`
- **管理员权限**: **否** (`Admin: False`)
- **评估**: 服务管理命令可用，但创建/删除服务需要管理员权限。如需运行 `install-service.ps1 -Install` 需在管理员 PowerShell 中执行

### 检查项 7: 项目构建状态
- **状态**: ✅ 通过
- **命令**: `dotnet build YLproxy.sln --configuration Debug`
- **结果**: **0 Error, 0 Warning**
- **构建时间**: ~4.6 秒

### 检查项 8: 项目测试状态
- **状态**: ✅ 通过
- **命令**: `dotnet test tests/YLproxy.Tests.csproj --configuration Debug`
- **结果**: **19 通过, 0 失败, 0 跳过**
- **测试耗时**: 682 ms

---

## 任务 2: 项目代码拍照

### 统计项 1: 各项目源代码统计

> 注: 仅统计手写源代码文件，排除 `bin/`、`obj/` 下自动生成的 `*AssemblyInfo.cs`、`*GlobalUsings.g.cs`、`*_wpftmp_*` 临时文件、以及 `App.g.cs` / `MainWindow.g.cs` / `AddProxyWindow.g.cs` / `MainView.g.cs` / `GeneratedInternalTypeHelper.g.cs` 等 XAML 生成文件

| 项目                    | .cs 文件数 | 总行数 |
|------------------------|-----------|--------|
| YLproxy.Models         | 3         | 139    |
| YLproxy.Utils          | 1         | 87     |
| YLproxy.Infrastructure | 7         | 432    |
| YLproxy.Core           | 4         | 394    |
| YLproxy.Proxy          | 4         | 609    |
| YLproxy.GUI            | 10        | 817    |
| **合计**               | **29**    | **2,478** |

### 统计项 2: MainViewModel.cs 数据

| 指标       | 值  |
|------------|-----|
| 文件路径   | `src/YLproxy.GUI/MainViewModel.cs` |
| 总行数     | 422 |
| 方法总数   | ~13 |
| 职责       | 主控制器：配置加载、代理生命周期、监控调度、仪表盘统计 |

### 统计项 3: data/config.json 数据结构

**顶层字段**:
- `Proxies` (array)

**ProxyItem 字段**:
| 字段名       | 类型     |
|-------------|----------|
| Id          | int      |
| Name        | string   |
| RemoteHost  | string   |
| RemotePort  | int      |
| Username    | string   |
| Password    | string   |
| LocalHost   | string   |
| LocalPort   | int      |
| Status      | int (enum: 0=Stopped, 1=Running, 2=Failed) |
| CreateTime  | DateTime |

### 统计项 4: .vscode/ 目录检查

- **状态**: ⚠️ `.vscode 目录不存在`
- **详情**: 项目中未找到 `.vscode/settings.json` 或 `.vscode/tasks.json`

---

## 汇总

| 检查项                              | 状态 |
|-------------------------------------|------|
| .NET SDK 10.0.301                   | ✅   |
| .NET Runtime 10.0.x                 | ✅   |
| 3proxy.exe (v0.9.7)                 | ✅   |
| 3proxy 必要 DLL                     | ✅   |
| Proxy 数据 (4条, 全部 Stopped)       | ✅   |
| Windows Service 基础设施             | ✅   |
| 管理员权限                          | ⚠️   |
| 项目构建 (0 Error, 0 Warning)       | ✅   |
| 项目测试 (19 通过)                   | ✅   |
| .vscode 目录                        | ⚠️   |

**通过率**: 10 项中 8 项通过，2 项注意（管理员权限缺失、.vscode 目录缺失均为非阻塞性问题）

**构建输出目录**:
- Debug: `src/YLproxy.GUI/bin/Debug/net10.0-windows/YLproxy.GUI.dll`
- Release: 尚未构建
