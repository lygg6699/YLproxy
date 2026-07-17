# TODO：提升覆盖率（ProxyTester / MonitorService / PreFlight 等）

**更新时间：2026-07-17**

## 目标
把覆盖率中 `YLproxy.Core.ProxyTester`、`YLproxy.Core.MonitorService`、`YLproxy.Core.PreFlight/*` 等模块当前的大量 `hits=0` 分支补齐，且不破坏现有行为与 E2E 测试。

## 计划步骤
- [ ] 1) 为 `ProxyTester` 增加“可注入 HTTP 执行器/ClientFactory”（默认行为保持不变）
- [ ] 2) 新增 `ProxyTesterTests`：覆盖 host 为空、认证信息不完整、取消/超时、HttpRequestException、通用异常、重试分支
- [ ] 3) 新增 `MonitorServiceTests`：覆盖 enumerate 失败、isRunning 抛异常、端口不可连导致 Failed、auto-restart backoff/超限/重启失败
- [ ] 4) 新增 `PreFlightTests`（含 AutoStartService）：覆盖 Passed/Errors/Warn 与 AutoStart 开关
- [ ] 5) 如覆盖率仍不足：为 `ProxyDataService` 补齐 JSON/异常/异常清理等分支
- [ ] 6) 跑 `dotnet test` + 重新生成 `coverage.cobertura.xml` 并验证覆盖率提升

