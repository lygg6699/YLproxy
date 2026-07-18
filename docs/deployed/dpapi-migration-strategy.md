# YLproxy DPAPI 凭据保护：跨用户 / 跨机器迁移策略

> 版本：P1 | 最后更新：2026-07-17
> 适用：YLproxy 0.2.0+

## DPAPI 绑定约束

`DpapiSecurityService` 使用 `System.Security.Cryptography.ProtectedData`（Windows DPAPI），绑定范围为 `DataProtectionScope.CurrentUser`：

| 约束 | 影响 |
|---|---|
| **用户绑定** | 仅加密时的 Windows 用户可解密 |
| **机器绑定** | 密钥派生自用户主密钥（存储在用户配置文件） |
| **无 optionalEntropy** | 当前实现不传递额外熵参数 |

这意味着以下场景会导致解密失败：

1. **不同 Windows 用户运行 YLproxy** → `CryptographicException: "decrypt for the current Windows user"`
2. **将 `config.json` 或 SQLite 数据库复制到另一台机器** → 同上
3. **用户配置文件被重置/重建** → DPAPI 主密钥丢失，所有加密凭据无法恢复

## 迁移前检查

在迁移前，读取当前 `config.json` 确认凭据状态：

```
# 检查当前加密状态（dpapi:v1: 前缀表示已加密）
grep -o "dpapi:v1:[A-Za-z0-9+/=]*" data\config.json
```

输出 `dpapi:v1:...` 表示凭据已加密。

## 场景一：重装系统（同一用户）

**前提**：保留当前 Windows 用户配置文件。

1. 备份 `data/config.json` 和 `data/ylproxy.db`（SQLite）
2. 重装系统后使用**同一 Microsoft/本地账号**登录
3. 恢复 `data/` 目录
4. 启动 YLproxy，应用自动解密

## 场景二：迁移到另一台机器

DPAPI 跨机器不可行。使用**导出/导入**流程：

### 2.1 导出（源机器）

在源机器启动 YLproxy GUI，使用导出功能：

- **导出含密码**：仅供迁移用，输出包含明文凭据的 JSON / CSV
- **导出不含密码**：日常使用，密码字段为 `****`

或通过 CLI 使用 REST API：

```sh
curl -H "Authorization: Bearer <token>" http://localhost:9100/api/proxies
```

> ⚠️ 以上接口返回的 `Password` 字段已脱敏为 `****`，不可用于迁移。

**推荐导出路线**：在 GUI 中选择"导出含密码"，保存到安全位置。

### 2.2 导入（目标机器）

1. 将导出的文件传输到目标机器
2. 在目标机器 YLproxy GUI 中手动添加代理配置
3. 或通过 API 批量导入：

```sh
curl -X POST http://localhost:9100/api/proxies \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"Name":"Proxy-1","RemoteHost":"192.168.1.1","RemotePort":8080,"Username":"user","Password":"pass"}'
```

> 存入后触发 `ProxyDataService.Save()` → `ProxyDataSerializer.Serialize()` 自动 DPAPI 加密。

## 场景三：Windows 用户切换 / 密码重置

**管理员重置用户密码**时，DPAPI 主密钥会**丢失**。

### 恢复步骤

1. 在 `AppSettings.json` 中启用启动时自动恢复（添加 `"ResetCredentials": true`）
2. 重启 YLproxy，观察启动日志
3. 如果 `CredentialsResetDuringLoad` 为 `true`，凭据已重置，需手动重新输入
4. 禁用 `ResetCredentials`

当前版本（0.2.0）不支持自动凭据重置。需手动：
- 删除 `data/config.json`（备份先）
- 重新添加所有代理配置

## 场景四：非 Windows 平台迁移

`DpapiSecurityService` 标记了 `[SupportedOSPlatform("windows")]`，构造函数在非 Windows 平台会抛出 `PlatformNotSupportedException`。

当前 YLproxy **仅支持 Windows**。如需跨平台，需要：
1. 实现 `ISecurityService` 的非 Windows 版本（如 AES + 密钥文件）
2. 或使用 DPAPI 替代方案（如 ASP.NET Data Protection API 的跨平台模式）

## 安全注意事项

- **导出的含密码文件**是明文凭据载体，传输后应立即删除
- **不要将含密码导出文件**存放在网络共享或云存储中
- SQLite 数据库 (`data/ylproxy.db`) 中的凭据也是 DPAPI 加密的，同样受用户/机器绑定
- 日志文件 (`logs/`) 已包含凭据脱敏过滤器，但不应用作凭据迁移源

## 参考

- [Microsoft Docs: Windows Data Protection](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection)
- `src/YLproxy.Infrastructure/DpapiSecurityService.cs` — 加密实现
- `src/YLproxy.Core/Config/ProxyDataSerializer.cs` — 序列化层（自动加解密）
- `tests/SecurityServiceTests.cs` — DPAPI 单元测试
