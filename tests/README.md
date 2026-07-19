# YLproxy.Test/

## 目录简介

测试项目目录，基于 xUnit，包含核心功能的单元测试与集成测试。

## 主要文件/子目录说明

测试按功能划分为多个测试类（如 `ProxyDataServiceTests`、`MonitorServiceTests`、
`ProxyTesterTests`、`SecurityServiceTests`、`ApiIntegrationTests`、`ProxyIntegrationTests`
等）。单元测试标注 `Trait("Category", "Unit")`，CI 默认只运行 `Category=Unit`。

> 说明：原先的手动控制台崩溃检测脚本 `Program.cs` 已删除。该脚本中硬编码了真实上游
> 代理凭据（安全风险），且已被 xUnit 中的 `MonitorServiceTests` / `LoggingAndMonitorTests`
> 覆盖同类场景，故不再保留。

## 使用说明

```powershell
# 运行全部单元测试
dotnet test tests/YLproxy.Tests.csproj --filter "Category=Unit"

# 运行全部测试（含集成测试，需 3proxy runtime 就绪）
dotnet test tests/YLproxy.Tests.csproj
```

## 注意事项

1. **测试隔离**：测试使用占位/私网地址（如 `127.0.0.1`、`10.0.0.x`）与占位凭据，
   禁止在测试中写入任何真实上游代理凭据。

2. **集成测试依赖**：部分集成/端到端测试依赖实际的 3proxy 可执行文件存在，未纳入
   CI 默认 `Category=Unit` 门禁。

## 后续计划

- 提升核心类的单元测试覆盖率
- 将集成/端到端测试纳入分层 CI 门禁
- 实现自动化的 UI 测试（使用 WinAppDriver 或类似工具）
- 添加性能和压力测试
