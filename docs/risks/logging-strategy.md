# YLproxy 日志生命周期与策略

> 版本：P2 | 最后更新：2026-07-16

## 日志分类与存储路径

| 类型 | 目录 | 文件命名 | 保留策略 | 清理方式 |
|---|---|---|---|---|
| **应用日志** | `logs/` | `log_yyyyMMdd.txt` | `RetentionDays` 天（默认 30） | `FileLogger` 构造时自动清理，基于 `LastWriteTime` |
| **3proxy 引擎日志** | `runtime/3proxy/logs/` | `3proxy-{id}.log` | 跟随代理生命周期 | 代理停止时保留，下次启动覆盖 |
| **3proxy 运行时配置** | `runtime/3proxy/cfg/` | `{id}.cfg` | 仅运行期间存在 | 代理停止时自动删除 |
| **测试临时日志** | 系统 `%TEMP%` 子目录 | 随机 UUID 目录 | 测试结束即清理 | `finally` 块递归删除 |

## 日志级别

| 级别 | 含义 | 生产默认 |
|---|---|---|
| `Debug` | 详细诊断信息（文件路径、进程状态等） | 关闭（需手动配置） |
| `Info` | 正常操作记录 | ✅ |
| `Warn` | 可恢复问题（清理失败、依赖检查降级等） | ✅ |
| `Error` | 需要关注的错误（启动失败、DB 错误等） | ✅ |
| `Fatal` | 导致进程终止的严重错误 | ✅ |

## 关键保护措施

1. **敏感信息不泄露**：日志不记录代理密码/加密后的凭据明文
2. **文件写入失败不阻塞**：`MainViewModel.AddLog()` 对文件写入采用 try-catch 保护
3. **循环依赖防护**：`LoggerFactory` 直接读取 `AppSettings.json`（不通过 `AppSettingsService`）避免构造期死锁
4. **全局异常兜底**：`App.xaml.cs` 注册 `DispatcherUnhandledException` / `UnhandledException` / `UnobservedTaskException`，未捕获异常写入 Fatal 级别日志并弹出提示

## 配置条目（AppSettings.json）

```json
{
  "Logging": {
    "LogDirectory": "logs",
    "RetentionDays": 30,
    "MinLevel": "Info"
  }
}
```

- `LogDirectory`：必须为 `logs`（强制验证）
- `RetentionDays`：≥ 0，为 0 时立即清理所有日志
- `MinLevel`：Debug / Info / Warn / Error / Fatal

## 日志不包含的内容

- 不使用结构化/JSON 格式（纯文本）
- 无相关性 ID（Correlation ID）
- 无请求追踪

> 以上功能预留 P3/P4 阶段评估。
