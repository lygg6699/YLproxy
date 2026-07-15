# YLproxy 3proxy 集成设计

> 3proxy 配置文件生成、进程管理、日志收集、故障恢复

版本：V1.0  
最后更新：2026-07-15

---

## 📋 目录

1. [3proxy 简介](#3proxy-简介)
2. [配置文件生成](#配置文件生成)
3. [进程生命周期管理](#进程生命周期管理)
4. [日志收集与分析](#日志收集与分析)
5. [常见问题](#常见问题)

---

## 3proxy 简介

### 什么是 3proxy？

**3proxy** 是一个轻量级的代理服务器，支持：
- ✅ HTTP/HTTPS 代理
- ✅ SOCKS4/SOCKS5 代理
- ✅ 认证管理
- ✅ 访问控制
- ✅ 日志记录

### YLproxy 中的角色

```
用户 → 本地端口 9001 → 3proxy.exe → 上游代理 → 目标网站
                    (进程)      (配置驱动)
```

### 为什么选择 3proxy？

- 独立可执行文件（无需安装）
- 配置简单
- 开源、稳定、性能好
- 占用资源少

---

## 配置文件生成

### ConfigGenerator.cs

**职责**：根据 ProxyItem 生成 3proxy.cfg 配置文件

#### 配置文件结构

```cfg
# runtime/3proxy/cfg/1.cfg
# 这是代理ID为1的配置文件

# 设置服务监听
service 9001

# 认证方式
auth strong

# 用户定义
users test_user:CL:test_password

# 访问控制
allow test_user

# 内部监听地址
internal 127.0.0.1 9001

# 上游代理地址
proxy 123.123.123.123 8000

# 刷新配置
flush
```

#### 生成算法

```csharp
public class ConfigGenerator
{
    public static string GenerateConfig(ProxyItem proxy)
    {
        var sb = new StringBuilder();
        
        // 1. 服务配置
        sb.AppendLine($"service {proxy.LocalPort}");
        sb.AppendLine();
        
        // 2. 认证方式
        sb.AppendLine("auth strong");
        
        // 3. 用户认证信息
        // 格式: users <username>:CL:<password>
        // CL = Clear (明文，3proxy 会加密存储)
        sb.AppendLine($"users {proxy.Username}:CL:{proxy.Password}");
        sb.AppendLine();
        
        // 4. 访问控制
        sb.AppendLine($"allow {proxy.Username}");
        sb.AppendLine();
        
        // 5. 内部监听地址
        sb.AppendLine($"internal {proxy.LocalHost} {proxy.LocalPort}");
        sb.AppendLine();
        
        // 6. 上游代理地址
        sb.AppendLine($"proxy {proxy.RemoteHost} {proxy.RemotePort}");
        sb.AppendLine();
        
        // 7. 刷新配置
        sb.AppendLine("flush");
        
        return sb.ToString();
    }
    
    public static void SaveConfig(ProxyItem proxy, string outputPath)
    {
        string config = GenerateConfig(proxy);
        File.WriteAllText(outputPath, config, Encoding.UTF8);
    }
}
```

#### 配置文件路径规则

```
runtime/3proxy/cfg/
├── 1.cfg              ← ProxyItem.Id = 1
├── 2.cfg              ← ProxyItem.Id = 2
├── 3.cfg              ← ProxyItem.Id = 3
└── ...
```

---

## 进程生命周期管理

### ProxyProcessManager.cs

**职责**：启动、停止、监控 3proxy.exe

#### 启动进程

```csharp
public class ProxyProcessManager : IProxyProcessManager
{
    private Dictionary<int, Process> _processes = new();  // 存储进程
    
    public async Task<bool> StartAsync(ProxyItem proxy)
    {
        try
        {
            // 1. 生成配置文件
            string cfgPath = Path.Combine("runtime/3proxy/cfg", $"{proxy.Id}.cfg");
            ConfigGenerator.SaveConfig(proxy, cfgPath);
            
            // 2. 构建启动参数
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine("runtime/3proxy", "3proxy.exe"),
                Arguments = cfgPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(cfgPath)
            };
            
            // 3. 启动进程
            var process = new Process { StartInfo = startInfo };
            
            // 4. 收集输出日志
            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Logger.Info($"[3proxy-{proxy.Id}] {e.Data}");
                }
            };
            
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Logger.Error($"[3proxy-{proxy.Id}] {e.Data}");
                }
            };
            
            // 5. 开始读取输出
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            // 6. 等待进程初始化
            await Task.Delay(1000);
            
            // 7. 检查进程是否还活着
            if (process.HasExited)
            {
                Logger.Error($"3proxy 进程立即退出，可能是配置错误");
                return false;
            }
            
            // 8. 存储进程引用
            _processes[proxy.Id] = process;
            
            // 9. 验证端口是否已监听
            if (!IsPortListening(proxy.LocalPort))
            {
                Logger.Warning($"端口 {proxy.LocalPort} 未被监听，启动可能有问题");
            }
            
            Logger.Info($"3proxy 进程启动成功 (PID: {process.Id}, Port: {proxy.LocalPort})");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"启动 3proxy 失败: {ex.Message}");
            return false;
        }
    }
}
```

#### 停止进程

```csharp
public async Task<bool> StopAsync(ProxyItem proxy)
{
    try
    {
        if (!_processes.TryGetValue(proxy.Id, out var process))
        {
            Logger.Warning($"进程不在追踪中 (ID: {proxy.Id})");
            return false;
        }
        
        if (process.HasExited)
        {
            Logger.Info($"进程已退出 (ID: {proxy.Id})");
            _processes.Remove(proxy.Id);
            return true;
        }
        
        // 1. 发送 SIGTERM 信号（正常终止）
        process.Kill(entireProcessTree: false);
        
        // 2. 等待进程退出（超时 5 秒）
        bool exited = process.WaitForExit(5000);
        
        if (!exited)
        {
            // 3. 强制杀死进程树
            process.Kill(entireProcessTree: true);
            process.WaitForExit(2000);
        }
        
        _processes.Remove(proxy.Id);
        
        Logger.Info($"3proxy 进程已停止 (ID: {proxy.Id})");
        return true;
    }
    catch (Exception ex)
    {
        Logger.Error($"停止 3proxy 失败: {ex.Message}");
        return false;
    }
}
```

#### 检查进程状态

```csharp
public bool IsRunning(ProxyItem proxy)
{
    try
    {
        if (!_processes.TryGetValue(proxy.Id, out var process))
        {
            return false;
        }
        
        // 检查进程是否还活着
        if (process.HasExited)
        {
            _processes.Remove(proxy.Id);
            return false;
        }
        
        return true;
    }
    catch
    {
        return false;
    }
}

private bool IsPortListening(int port)
{
    try
    {
        var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
        var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();
        
        return tcpConnInfoArray.Any(endpoint => endpoint.Port == port);
    }
    catch
    {
        return false;  // 无法判断时假设不在监听
    }
}
```

---

## 日志收集与分析

### 日志输出位置

| 来源 | 位置 | 用途 |
|---|---|---|
| **3proxy 标准输出** | 捕获 | 启动/关闭信息 |
| **3proxy 标准错误** | 捕获 | 错误信息 |
| **YLproxy 应用日志** | logs/ylproxy.log | 所有操作记录 |

### 日志示例

```log
[2026-07-15 10:30:25] [INFO] [MonitorService] 启动状态监控
[2026-07-15 10:30:26] [INFO] [MainViewModel] 用户点击"启动代理"
[2026-07-15 10:30:26] [INFO] [ProxyProcessManager] 生成配置文件: runtime/3proxy/cfg/1.cfg
[2026-07-15 10:30:27] [INFO] [3proxy-1] 3proxy version 0.8.12
[2026-07-15 10:30:27] [INFO] [3proxy-1] Listening on 127.0.0.1:9001
[2026-07-15 10:30:27] [INFO] [ProxyProcessManager] 3proxy 进程启动成功 (PID: 12345, Port: 9001)
[2026-07-15 10:30:28] [INFO] [ProxyTester] 代理测试请求发送
[2026-07-15 10:30:29] [INFO] [ProxyTester] 代理测试成功，延迟: 150ms
[2026-07-15 10:30:35] [INFO] [MonitorService] 检测代理状态: 代理1 运行中
```

### 日志分析

使用日志文件诊断问题：

```powershell
# 查看最近 50 行日志
Get-Content "logs/ylproxy.log" -Tail 50

# 查看错误日志
Select-String "ERROR" "logs/ylproxy.log"

# 查看特定代理的日志
Select-String "\[3proxy-1\]" "logs/ylproxy.log"
```

---

## 常见问题

### Q1：配置文件生成后，3proxy 无法启动

**排查步骤**：
1. 检查配置文件格式是否正确
2. 检查用户名/密码是否包含特殊字符
3. 查看 3proxy 的标准错误输出
4. 尝试手动运行 `3proxy.exe cfg/1.cfg`

### Q2：进程启动后立即退出

**常见原因**：
- ❌ 配置文件有语法错误
- ❌ 本地端口已被占用
- ❌ 权限不足
- ❌ 3proxy.exe 文件损坏

**解决方案**：
```powershell
# 1. 检查端口是否被占用
netstat -ano | findstr :9001

# 2. 手动测试 3proxy
cd runtime/3proxy
.\3proxy.exe cfg/1.cfg
```

### Q3：如何清理孤儿进程？

```powershell
# 查找所有 3proxy 进程
Get-Process | Where-Object { $_.Name -like "*3proxy*" }

# 强制杀死进程
Stop-Process -Name 3proxy -Force
```

---

## 📖 相关文档

- [后端架构.md](后端架构.md)
- [04-核心功能/代理转换原理.md](../04-核心功能/代理转换原理.md)

---

**最后更新**：2026-07-15
