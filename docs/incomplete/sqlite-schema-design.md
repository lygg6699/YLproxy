# YLproxy SQLite 数据库 Schema 设计文档

**文档版本：** 1.0  
**创建日期：** 2026-07-16  
**最后更新：** 2026-07-19  
**用途：** 执行师 1 实现 `SqliteProxyRepository.cs` 和 `DataMigrationService.cs` 的权威参考  
**前置阅读：** `docs/architecture-analysis-report-20260716.md`  
**决策状态：** 🟡 待决策 - 当前使用 JSON 存储，本方案为备用方案，需在 Phase C1.1 中决策是否实施

---

## 目录

1. [设计目标](#1-设计目标)
2. [数据库文件规范](#2-数据库文件规范)
3. [表结构定义](#3-表结构定义)
4. [CREATE TABLE 语句](#4-create-table-语句)
5. [迁移策略](#5-迁移策略)
6. [双写过渡期策略](#6-双写过渡期策略)
7. [与现有 ProxyItem 模型的映射关系](#7-与现有-proxyitem-模型的映射关系)
8. [安全注意事项](#8-安全注意事项)

---

## 1. 设计目标

### 1.1 迁移目的

当前 YLproxy 使用 `data/config.json` 作为代理配置的持久化存储。JSON 文件存储存在以下局限：

- **并发写入风险：** 多操作并发时可能出现数据覆盖或损坏。
- **查询效率低：** 全量加载/保存模式，无索引支持，无法按条件筛选。
- **无事务保障：** 写入中途崩溃可能导致数据丢失或不一致。
- **扩展性差：** 后续分页、搜索、统计等功能难以高效实现。

**当前状态：** JSON 存储已实现原子写入、备份轮转、错误恢复机制，在单机场景下基本满足需求。SQLite 迁移作为备用方案，在 Phase C1.1 中进行决策。

SQLite 作为嵌入式关系型数据库，可以在不引入外部服务的前提下，解决上述问题。

### 1.2 设计原则

| 原则 | 说明 |
|------|------|
| **零破坏现有功能** | 迁移后的运行行为与 JSON 存储时期保持完全一致 |
| **双写过渡期** | 迁移完成后 JSON 和 SQLite 同时写入，确保可回滚 |
| **可回滚** | 保留 JSON 备份，SQLite 损坏时自动降级读取 JSON |
| **最小依赖** | 仅引入 `Microsoft.Data.Sqlite` NuGet 包，不引入 ORM |
| **安全意识** | 凭据字段级 DPAPI 加密，SQLite 文件不加密码 |
| **决策驱动** | 仅在确定需要复杂查询和并发场景时实施，否则保持 JSON 优化方案 |

---

## 2. 数据库文件规范

### 2.1 文件路径

| 属性 | 值 |
|------|-----|
| **文件名** | `ylproxy.db` |
| **目录** | `data/` |
| **完整相对路径** | `data/ylproxy.db` |
| **C# 解析方式** | `YLproxy.Utils.PathResolver.ResolvePath("data", "ylproxy.db")` |

### 2.2 连接字符串

```
Data Source=<PathResolver.ResolvePath("data", "ylproxy.db")的绝对路径>
```

示例（假设仓库根目录为 `C:\Users\xxx\YLproxy`）：

```
Data Source=C:\Users\xxx\YLproxy\data\ylproxy.db
```

### 2.3 WAL 日志模式

连接建立后立即执行：

```sql
PRAGMA journal_mode=WAL;
```

**WAL 模式优势：**
- 读操作不阻塞写操作，写操作不阻塞读操作
- 适用于 MonitorService 后台读取 + GUI 操作写入的并发场景
- 崩溃恢复能力优于默认的 DELETE 模式

### 2.4 其他 PRAGMA 设置

```sql
PRAGMA foreign_keys=ON;
PRAGMA busy_timeout=3000;
```

---

## 3. 表结构定义

### 3.1 表名：`proxies`

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| `Id` | INTEGER | PRIMARY KEY AUTOINCREMENT | 代理唯一标识，自增。与 `ProxyItem.Id` 对应 |
| `Name` | TEXT | NOT NULL | 代理名称，对应 `ProxyItem.Name` |
| `RemoteHost` | TEXT | NOT NULL | 远程代理主机地址，对应 `ProxyItem.RemoteHost` |
| `RemotePort` | INTEGER | NOT NULL | 远程代理端口，范围 1-65535，对应 `ProxyItem.RemotePort` |
| `Username` | TEXT | NOT NULL DEFAULT '' | **加密后**的用户名（`dpapi:v1:` 前缀），对应 `ProxyItem.Username` |
| `Password` | TEXT | NOT NULL DEFAULT '' | **加密后**的密码（`dpapi:v1:` 前缀），对应 `ProxyItem.Password` |
| `LocalHost` | TEXT | NOT NULL DEFAULT '127.0.0.1' | 本地监听地址，对应 `ProxyItem.LocalHost` |
| `LocalPort` | INTEGER | NOT NULL UNIQUE | 本地监听端口，UNIQUE 约束防止端口冲突，对应 `ProxyItem.LocalPort` |
| `Status` | TEXT | NOT NULL DEFAULT 'Stopped' | 代理状态，CHECK 约束限制为 `Stopped`/`Running`/`Failed` |
| `CreateTime` | TEXT | NOT NULL | 创建时间，ISO 8601 格式（如 `2026-07-16T12:00:00Z`），对应 `ProxyItem.CreateTime` |
| `UpdateTime` | TEXT | NOT NULL | 最后更新时间，ISO 8601 格式。创建时与 `CreateTime` 相同，每次修改时更新 |

### 3.2 附加约束说明

- **LocalPort UNIQUE：** 每个本地端口只能被一个代理使用，UNIQUE 约束在数据库层面强制执行。
- **Status CHECK：** 只允许 `Stopped`、`Running`、`Failed` 三个字符串值，与 `ProxyStatus` 枚举的三个成员一一对应。
- **Status 索引：** `Status` 列上创建索引，用于 `MonitorService` 按状态筛选的高频查询（如 `WHERE Status = 'Running'`）。

### 3.3 关于 Username / Password 加密

Username 和 Password 在**写入数据库之前**已经由 `ISecurityService.Encrypt()` 加密，格式为 `dpapi:v1:<base64-ciphertext>`。

数据库本身**不感知加密逻辑**——它只是存储 TEXT。读写流程：

```
写入：ProxyItem.Username (明文) → ISecurityService.Encrypt() → proxies.Username (密文)
读取：proxies.Username (密文) → ISecurityService.Decrypt() → ProxyItem.Username (明文)
```

---

## 4. CREATE TABLE 语句

以下 SQL 语句必须**原样**使用，不得修改表名、列名、类型或约束：

```sql
CREATE TABLE IF NOT EXISTS proxies (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Name        TEXT    NOT NULL,
    RemoteHost  TEXT    NOT NULL,
    RemotePort  INTEGER NOT NULL,
    Username    TEXT    NOT NULL DEFAULT '',
    Password    TEXT    NOT NULL DEFAULT '',
    LocalHost   TEXT    NOT NULL DEFAULT '127.0.0.1',
    LocalPort   INTEGER NOT NULL UNIQUE,
    Status      TEXT    NOT NULL DEFAULT 'Stopped' CHECK(Status IN ('Stopped','Running','Failed')),
    CreateTime  TEXT    NOT NULL,
    UpdateTime  TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_proxies_status ON proxies(Status);
```

### 4.1 设计决策记录

| 决策 | 理由 |
|------|------|
| 时间存储为 TEXT 而非 INTEGER (Unix timestamp) | 与 JSON 格式保持一致，便于人工调试和迁移验证 |
| Id 使用 AUTOINCREMENT 而非手动指派 | 避免与 JSON 中的 Id 值冲突，迁移后由 SQLite 自动分配新 Id |
| 密码不单独建表 | 凭据已由 DPAPI 加密，无需额外隔离；保持与 JSON 结构一对一的简单映射 |
| 不设外键 | 当前仅一张表，无关联表，外键为过度设计 |

---

## 5. 迁移策略

### 5.1 迁移标记文件

| 属性 | 值 |
|------|-----|
| **文件路径** | `data/.migration_completed` |
| **含义** | 存在即表示 JSON → SQLite 迁移已成功完成 |
| **内容** | 写入迁移完成时的 ISO 8601 时间戳和迁移记录数（纯文本） |

示例内容：
```
2026-07-16T12:00:00Z
Migrated 2 proxies from config.json to ylproxy.db
```

### 5.2 迁移触发时机

**触发位置：** 应用启动时，在 `ProxyDataService` 初始化阶段（构造函数或 `InitializeAsync` 方法中）。

**触发条件（三个条件全部满足）：**

1. `data/config.json` 文件存在且可读
2. `data/.migration_completed` 标记文件**不存在**
3. `data/ylproxy.db` 不存在 **或** 存在但 `proxies` 表为空（`SELECT COUNT(*) FROM proxies` 返回 0）

> 注意：如果 `data/config.json` 不存在但 `data/ylproxy.db` 存在（例如全新克隆后首次运行），则跳过迁移，直接使用 SQLite。

### 5.3 迁移步骤（按顺序执行）

```
Step 1: 备份
  源文件: data/config.json
  目标文件: data/config.json.migration.bak
  操作: 复制（非移动）config.json 到 .migration.bak

Step 2: 创建数据库
  路径: data/ylproxy.db
  操作: 创建 SQLite 数据库文件，执行 PRAGMA journal_mode=WAL，执行建表语句

Step 3: 逐条迁移
  读取: data/config.json → 解析 AppConfig.Proxies 数组
  对每条 ProxyItem：
    a. 调用 ISecurityService.Encrypt() 加密 Username 和 Password
    b. 将 Status 枚举转为字符串（Stopped/Running/Failed）
    c. 将 CreateTime 和 UpdateTime 转为 ISO 8601 字符串
    d. INSERT 到 proxies 表
  使用事务包裹全部 INSERT 操作

Step 4: 验证
  对比: JSON 中 Proxies 数组长度 == SQLite 中 SELECT COUNT(*) FROM proxies
  不一致 → 触发回滚

Step 5: 创建标记文件
  创建: data/.migration_completed
  内容: 当前时间戳 + 迁移记录数
```

### 5.4 迁移失败回滚

任何步骤失败时的回滚操作（按顺序）：

1. 删除 `data/ylproxy.db`（如果已创建）
2. 删除 `data/.migration_completed`（如果已创建）
3. **保留** `data/config.json.migration.bak`（作为恢复凭据，供人工排查）
4. 抛出异常，阻止应用继续启动（应用在启动阶段检测到迁移失败应终止并提示用户）

### 5.5 幂等性保证

- 标记文件 `data/.migration_completed` 确保迁移只执行一次
- 即使标记文件被误删，如果 `ylproxy.db` 已有数据，条件 3（表为空）不满足，不会重复迁移
- 如果需要强制重新迁移：手动删除 `data/ylproxy.db` 和 `data/.migration_completed`，保留 `data/config.json`

---

## 6. 双写过渡期策略

### 6.1 策略概述

迁移完成后，应用进入**双写过渡期**：所有写操作同时写入 SQLite 和 JSON，读操作优先从 SQLite 读取。

### 6.2 读写规则

| 操作 | 行为 |
|------|------|
| **Load（加载全部代理）** | 从 SQLite `proxies` 表读取；如果 SQLite 不可用，回退读取 `data/config.json` |
| **Save（保存全部代理）** | 先写入 SQLite（事务），成功后写入 JSON（原子替换）；SQLite 写入失败则整体失败 |
| **Add（添加单个代理）** | INSERT 到 SQLite，然后触发一次全量 Save 同步 JSON |
| **Update（更新单个代理）** | UPDATE SQLite，然后触发一次全量 Save 同步 JSON |
| **Delete（删除单个代理）** | DELETE FROM SQLite，然后触发一次全量 Save 同步 JSON |

### 6.3 回退机制

如果 SQLite 数据库文件损坏（打开连接时抛出异常）或 `proxies` 表不存在：

```
1. 记录错误日志："SQLite database unavailable, falling back to JSON"
2. 从 data/config.json 加载数据
3. 后续写操作仅写入 JSON（不再尝试 SQLite）
4. 在 UI 中提示用户"数据库异常，已降级为文件存储模式"
```

### 6.4 过渡期结束条件

满足以下任一条件后，在后续版本中移除 JSON 读写路径：

- 至少经过一个完整版本周期（v0.3.0 → v0.4.0），且无用户报告数据不一致问题
- 所有用户均已完成迁移（通过遥测或日志确认）
- `data/.migration_completed` 在用户设备上存在超过 30 天

### 6.5 过渡期结束后的清理

- 移除 `ProxyDataSerializer` 对 JSON 的写入路径（保留读取能力用于回退）
- 将 `data/config.json` 标记为 deprecated，后续版本仅作为只读备份
- 移除双写逻辑中的 JSON Save 调用

---

## 7. 与现有 ProxyItem 模型的映射关系

### 7.1 字段映射表

| C# `ProxyItem` 属性 | C# 类型 | `proxies` 表列 | SQLite 类型 | 转换说明 |
|---------------------|---------|---------------|-------------|---------|
| `Id` | `int` | `Id` | INTEGER | 直接映射。迁移时 JSON 中的 Id 值写入 SQLite；新创建的代理由 AUTOINCREMENT 生成 |
| `Name` | `string` | `Name` | TEXT | 直接映射 |
| `RemoteHost` | `string` | `RemoteHost` | TEXT | 直接映射 |
| `RemotePort` | `int` | `RemotePort` | INTEGER | 直接映射 |
| `Username` | `string` | `Username` | TEXT | **写入时：** `ISecurityService.Encrypt(Username)` → TEXT；**读取时：** TEXT → `ISecurityService.Decrypt()` → 明文 |
| `Password` | `string` | `Password` | TEXT | **写入时：** `ISecurityService.Encrypt(Password)` → TEXT；**读取时：** TEXT → `ISecurityService.Decrypt()` → 明文 |
| `LocalHost` | `string` | `LocalHost` | TEXT | 直接映射 |
| `LocalPort` | `int` | `LocalPort` | INTEGER | 直接映射。数据库层 UNIQUE 约束在 C# 层也应有对应的端口冲突检测 |
| `Status` | `ProxyStatus` (enum) | `Status` | TEXT | **写入时：** `Status.ToString()` → TEXT；**读取时：** `Enum.Parse<ProxyStatus>(text)` |
| `CreateTime` | `DateTime` | `CreateTime` | TEXT | **写入时：** `CreateTime.ToString("O")` → ISO 8601；**读取时：** `DateTime.Parse(text)` |
| *(不存在)* | — | `UpdateTime` | TEXT | **仅数据库有此列**，`ProxyItem` 中不存储。写入时设为 `DateTime.UtcNow.ToString("O")` |

### 7.2 ProxyStatus 枚举映射

| C# `ProxyStatus` | `proxies.Status` 字符串值 |
|-------------------|--------------------------|
| `ProxyStatus.Stopped` (0) | `"Stopped"` |
| `ProxyStatus.Running` (1) | `"Running"` |
| `ProxyStatus.Failed` (2) | `"Failed"` |

### 7.3 读写代码模式（参考）

```csharp
// 写入：ProxyItem → SQLite
var parameters = new Dictionary<string, object>
{
    ["@Name"] = item.Name,
    ["@RemoteHost"] = item.RemoteHost,
    ["@RemotePort"] = item.RemotePort,
    ["@Username"] = _securityService.Encrypt(item.Username),  // 加密
    ["@Password"] = _securityService.Encrypt(item.Password),  // 加密
    ["@LocalHost"] = item.LocalHost,
    ["@LocalPort"] = item.LocalPort,
    ["@Status"] = item.Status.ToString(),                     // 枚举→字符串
    ["@CreateTime"] = item.CreateTime.ToString("O"),           // DateTime→ISO 8601
    ["@UpdateTime"] = DateTime.UtcNow.ToString("O")
};

// 读取：SQLite → ProxyItem
var item = new ProxyItem
{
    Id = reader.GetInt32(0),
    Name = reader.GetString(1),
    RemoteHost = reader.GetString(2),
    RemotePort = reader.GetInt32(3),
    Username = _securityService.Decrypt(reader.GetString(4)),  // 解密
    Password = _securityService.Decrypt(reader.GetString(5)),  // 解密
    LocalHost = reader.GetString(6),
    LocalPort = reader.GetInt32(7),
    Status = Enum.Parse<ProxyStatus>(reader.GetString(8)),     // 字符串→枚举
    CreateTime = DateTime.Parse(reader.GetString(9))
};
```

---

## 8. 安全注意事项

### 8.1 SQLite 文件安全

- **不加数据库密码：** 凭据已由 Windows DPAPI 逐字段加密存储，对数据库文件加密码是冗余的。
- **文件权限：** `data/ylproxy.db` 继承 `data/` 目录的 NTFS 文件权限。由于 DPAPI 使用 `CurrentUser` 作用域，即使数据库