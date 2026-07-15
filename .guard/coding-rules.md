# C# 编码规范（通用）

> 适用于任何 .NET 项目的 C# 代码编写标准和约定

版本：V1.0  
生效日期：2026-07-15

---

## 📋 目录

1. [命名规范](#命名规范)
2. [代码组织](#代码组织)
3. [异常处理](#异常处理)
4. [日志规范](#日志规范)
5. [配置管理](#配置管理)
6. [依赖注入](#依赖注入)

---

## 命名规范

### 类和接口

```csharp
// ✅ 类：PascalCase
public class ProxyTester { }
public class ProxyProcessManager { }
public class MainViewModel { }

// ✅ 接口：IXxx 前缀 + PascalCase
public interface IProxyTester { }
public interface ILogger { }
public interface ISecurityService { }

// ❌ 禁止：camelCase
public class proxyTester { }  // 错误

// ❌ 禁止：前缀无关意义
public class Proxy_Tester { }  // 错误
```

### 方法

```csharp
// ✅ 公开方法：PascalCase
public async Task<ProxyTestResult> TestAsync(ProxyItem proxy) { }
public void StartProxy(int proxyId) { }
public bool IsPortInUse(int port) { }

// ✅ 异步方法：Async 后缀
public async Task SaveConfigAsync() { }
public async Task LoadProxiesAsync() { }

// ✅ 布尔方法：Is/Has/Can 前缀
public bool IsRunning(ProxyItem proxy) { }
public bool HasExited(Process process) { }
public bool CanAllocatePort(int port) { }

// ❌ 禁止：无意义的前缀
public void get_proxy() { }  // 错误
```

### 属性和字段

```csharp
public class ProxyItem
{
    // ✅ 公开属性：PascalCase
    public int Id { get; set; }
    public string Name { get; set; }
    public ProxyStatus Status { get; set; }
    
    // ✅ 私有字段：_camelCase 前缀
    private readonly ILogger _logger;
    private readonly Dictionary<int, Process> _processes;
    private int _retryCount;
    
    // ❌ 禁止：混乱命名
    public int id { get; set; }  // 错误
    private int process_count;   // 错误
    private ILogger logger;       // 错误（缺少下划线）
}
```

### 常量和枚举

```csharp
// ✅ 常量：UPPER_SNAKE_CASE
public const int DEFAULT_TIMEOUT = 30000;  // 毫秒
public const int MAX_RETRY_COUNT = 3;
public const string DEFAULT_CONFIG_FILE = "config.json";

// ✅ 枚举值：PascalCase
public enum ConnectionStatus
{
    Idle,
    Connecting,
    Connected,
    Disconnecting,
    Disconnected,
    Error
}

// ❌ 禁止：小写
public const int defaultTimeout = 30000;  // 错误

// ❌ 禁止：混乱
public const int DefaultTimeout = 30000;  // 这是常量，应该大写
```

---

## 代码组织

### 文件和类的组织

```csharp
// 一个文件一个公开类，异常情况可包含相关的支持类

namespace MyProject.Services
{
    // 1. Using 语句
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    
    // 2. 命名空间说明
    
    /// <summary>
    /// 公开类的 XML 文档注释
    /// </summary>
    public class DataService
    {
        // 3. 字段（私有）
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        
        // 4. 构造函数
        public DataService(ILogger logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }
        
        // 5. 属性
        public int Timeout { get; set; }
        
        // 6. 公开方法
        public async Task<ServiceResult> ProcessAsync(DataModel data)
        {
            // 实现
        }
        
        // 7. 私有方法
        private bool ValidateData(DataModel data)
        {
            // 实现
        }
    }
    
    // 8. 支持类（同文件内）
    public class ServiceResult
    {
        public bool Success { get; set; }
        public int DelayMs { get; set; }
        public string ErrorMessage { get; set; }
    }
}
```

### 代码行长度

```
推荐：< 120 字符
最大：不超过 150 字符

示例（✅ 推荐）：
return proxies
    .Where(p => p.Status == ProxyStatus.Running)
    .OrderBy(p => p.LocalPort)
    .ToList();

示例（❌ 禁止）：
return proxies.Where(p => p.Status == ProxyStatus.Running && p.LocalPort > 9000 && p.LocalPort < 9100).OrderBy(p => p.LocalPort).ToList();  // 太长！
```

---

## 异常处理

### 规则 1：不允许吞没异常

```csharp
// ❌ 禁止：空的 catch 块
try
{
    LoadConfig();
}
catch (Exception)
{
    // 吞没异常，无人知道发生了什么
}

// ✅ 推荐：记录后重新抛出
try
{
    LoadConfig();
}
catch (Exception ex)
{
    _logger.Error($"加载配置失败: {ex.Message}");
    throw;  // 重新抛出
}

// ✅ 也可以：捕获特定异常并处理
try
{
    LoadConfig();
}
catch (FileNotFoundException ex)
{
    _logger.Warning($"配置文件不存在: {ex.FileName}");
    // 创建默认配置
    CreateDefaultConfig();
}
```

### 规则 2：使用特定异常

```csharp
// ❌ 禁止：捕获所有异常再转换
try
{
    var port = int.Parse(portString);
}
catch (Exception ex)
{
    throw new ApplicationException("端口号无效", ex);
}

// ✅ 推荐：直接捕获预期的异常
try
{
    var port = int.Parse(portString);
}
catch (FormatException ex)
{
    _logger.Error($"端口号格式错误: {portString}");
    throw new ApplicationException("端口号无效", ex);
}
catch (OverflowException ex)
{
    _logger.Error($"端口号超出范围: {portString}");
    throw new ApplicationException("端口号超出范围", ex);
}
```

### 规则 3：异常链

```csharp
// ✅ 保留原始异常信息
catch (Exception ex)
{
    _logger.Error($"操作失败: {ex.Message}");
    throw new ApplicationException("自定义消息", ex);  // ← 包含 ex
}

// ❌ 禁止：丢失原始异常
catch (Exception ex)
{
    throw new ApplicationException("自定义消息");  // ❌ 丢失了 ex
}
```

---

## 日志规范

### 日志等级使用

```csharp
// ✅ DEBUG：详细的调试信息
_logger.Debug($"解析配置: {configPath}");
_logger.Debug($"代理状态检查: {proxy.Name}, Running={isRunning}");

// ✅ INFO：重要的业务信息
_logger.Info($"应用启动完成");
_logger.Info($"代理已启动: {proxy.Name}");

// ✅ WARNING：警告信息（不影响功能）
_logger.Warning($"端口 {port} 已被占用，尝试其他端口");
_logger.Warning($"配置版本过旧，建议更新");

// ✅ ERROR：错误信息（影响功能或流程）
_logger.Error($"启动 3proxy 失败: {ex.Message}");
_logger.Error($"保存配置异常: {ex.Message}");
```

### 日志内容规范

```csharp
// ✅ 包含足够的上下文
_logger.Info($"代理 '{proxy.Name}' (ID={proxy.Id}) 已启动，监听端口 {proxy.LocalPort}");

// ✅ 错误日志包含异常详情
try { }
catch (Exception ex)
{
    _logger.Error($"操作失败: {ex.GetType().Name} - {ex.Message}");
}

// ❌ 禁止：无意义的日志
_logger.Info("OK");
_logger.Info("Done");

// ❌ 禁止：日志过于啰嗦
_logger.Info("第一步完成");
_logger.Info("第二步开始");
_logger.Info("第二步完成");  // 太多细节
```

### 敏感信息

```csharp
// ❌ 禁止：记录密码
_logger.Info($"使用认证信息登录: {username}:{password}");

// ✅ 推荐：只记录非敏感部分
_logger.Info($"使用认证信息登录: {username}");

// ✅ 推荐：记录掩码信息
string maskedPassword = new string('*', password.Length);
_logger.Debug($"密码已设置 (长度: {password.Length})");
```

---

## 配置管理

### 规则 1：不允许硬编码配置

```csharp
// ❌ 禁止：硬编码路径
string configPath = "C:\\data\\config.json";
int minPort = 9001;

// ✅ 推荐：从配置读取
string configPath = _appSettings.Proxy.ConfigPath;
int minPort = _appSettings.Proxy.MinPort;

// ✅ 或使用依赖注入
public ProxyTester(IAppSettings appSettings)
{
    _minPort = appSettings.Proxy.MinPort;
    _configPath = appSettings.Proxy.ConfigPath;
}
```

### 规则 2：配置的默认值

```csharp
// ✅ 在配置类中定义默认值
public class ProxySettings
{
    public int MinPort { get; set; } = 9001;   // 默认值
    public int MaxPort { get; set; } = 9100;   // 默认值
    public int TimeoutSeconds { get; set; } = 10;
}
```

### 规则 3：配置验证

```csharp
// ✅ 验证配置有效性
public class ProxySettings
{
    private int _minPort;
    public int MinPort
    {
        get => _minPort;
        set
        {
            if (value < 1 || value > 65535)
                throw new ArgumentException("端口号无效");
            _minPort = value;
        }
    }
}
```

---

## 依赖注入

### 规则 1：构造函数注入

```csharp
// ✅ 推荐：构造函数注入
public class ProxyService
{
    private readonly ILogger _logger;
    private readonly IProxyDataService _dataService;
    
    public ProxyService(ILogger logger, IProxyDataService dataService)
    {
        _logger = logger;
        _dataService = dataService;
    }
}

// ❌ 禁止：属性注入（难以测试）
public class ProxyService
{
    public ILogger Logger { get; set; }
    public IProxyDataService DataService { get; set; }
}
```

### 规则 2：依赖抽象

```csharp
// ✅ 依赖接口
public class ProxyTester
{
    private readonly IHttpClient _httpClient;  // 接口
    
    public ProxyTester(IHttpClient httpClient)
    {
        _httpClient = httpClient;
    }
}

// ❌ 禁止：直接依赖具体类
public class ProxyTester
{
    private readonly HttpClient _httpClient;  // 具体类
}
```

---

## 📖 相关文档

- [guard.md](guard.md) - 开发守护协议
- [workflow.md](workflow.md) - 开发流程
- [test-rules.md](test-rules.md) - 测试规范

---

**最后更新**：2026-07-15
