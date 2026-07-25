# 阶段一：稳定性加固执行方案

> **执行环境：** Windows本地
> **预计工时：** 40小时
> **进度目标：** 65% → 75%
> **执行人：** 本地AI

---

## 执行前准备

### 环境检查
```powershell
# 确认当前分支
git branch

# 确认工作区干净
git status

# 拉取最新代码
git pull origin main

# 创建功能分支
git checkout -b phase1-stability-enhancement
```

### 备份当前状态
```powershell
# 创建备份标签
git tag backup-before-phase1

# 备份关键配置文件
Copy-Item data\config.json data\config.json.backup -ErrorAction SilentlyContinue
Copy-Item AppSettings.json AppSettings.json.backup -ErrorAction SilentlyContinue
```

---

## 子阶段 1.1：异常处理治理（65% → 68%）

### 目标
- 治理所有空 catch 块
- 补充 ILogger 错误记录
- 添加适当的异常处理逻辑
- 补充单元测试验证异常处理

### 执行步骤

#### 步骤 1.1.1：搜索所有空 catch 块
```powershell
# 在项目根目录执行
cd e:\GZQ\YLXCX\YLproxy

# 搜索空 catch 块模式
Select-String -Path "src\**\*.cs" -Pattern "catch\s*\(\s*\)\s*\{\s*\}" -AllMatches | Select-Object Path, LineNumber, Line
```

**预期输出：** 应该找到约 13 处空 catch 块

#### 步骤 1.1.2：逐个文件审查和修复

**文件 1：`src/YLproxy.GUI/MainViewModel.cs`**
```csharp
// 定位第 794-799 行的空 catch 块
// 原代码：
try { _logger.Info(message); }
catch
{
    // Logging failure is non-critical; swallow to avoid crashing the application.
    System.Diagnostics.Debug.WriteLine($"AddLog: failed to write log entry: {message}");
}

// 修复为：
try { _logger.Info(message); }
catch (Exception ex)
{
    // Logging failure is non-critical; swallow to avoid crashing the application.
    System.Diagnostics.Debug.WriteLine($"AddLog: failed to write log entry: {message}");
    _logger.Warn($"Logging failed in AddLog: {ex.Message}");
}
```

**文件 2：`src/YLproxy.Core/ProxyProcessManager.cs`**
```csharp
// 搜索并修复所有空 catch 块
// 添加具体的异常类型和日志记录
```

**文件 3：`src/YLproxy.Infrastructure/FileLogger.cs`**
```csharp
// 搜索并修复所有空 catch 块
// 添加文件系统异常的特殊处理
```

**通用修复模式：**
```csharp
// 原模式：
catch { }

// 修复模式：
catch (Exception ex)
{
    _logger.Error($"Operation failed in [ClassName.MethodName]: {ex.Message}");
    // 根据业务逻辑决定是否需要降级处理或重试
}
```

#### 步骤 1.1.3：补充异常处理单元测试
```powershell
# 创建新测试文件
New-Item -Path "tests\ExceptionHandlingTests.cs" -ItemType File
```

**测试文件内容模板：**
```csharp
using Xunit;
using YLproxy.Infrastructure;
using System;

namespace YLproxy.Tests;

public class ExceptionHandlingTests
{
    [Fact]
    public void MainViewModel_AddLog_LoggingFailure_ShouldNotCrash()
    {
        // Arrange
        var logger = new TestLogger(shouldFail: true);
        
        // Act & Assert
        var exception = Record.Exception(() => logger.Info("test message"));
        Assert.Null(exception); // 应该不抛出异常
    }

    [Fact]
    public void ProxyProcessManager_Start_ProcessStartFailure_ShouldLogError()
    {
        // Arrange
        // Act & Assert
        // 验证进程启动失败时正确记录错误日志
    }
}
```

#### 步骤 1.1.4：验证修复
```powershell
# 编译项目
dotnet build YLproxy.sln

# 运行测试
dotnet test tests/YLproxy.Tests.csproj --filter "FullyQualifiedName~ExceptionHandling"

# 运行全部测试
dotnet test tests/YLproxy.Tests.csproj
```

#### 步骤 1.1.5：提交代码
```powershell
git add src/ tests/
git commit -m "[Phase 1.1] Exception handling治理 - 修复空catch块并补充日志记录"
git push origin phase1-stability-enhancement
```

---

## 子阶段 1.2：数据持久化策略决策（68% → 70%）

### 目标
- 评估当前 JSON 方案的并发性能
- 决策保持 JSON + 优化并发控制
- 实现文件锁机制防止并发冲突

### 执行步骤

#### 步骤 1.2.1：评估当前 JSON 实现
```powershell
# 检查当前的 ProxyDataService 实现
Get-Content src\YLproxy.Core\ProxyDataService.cs
```

**分析要点：**
- 当前是否有并发保护机制
- 文件读写是否原子化
- 是否存在竞态条件风险

#### 步骤 1.2.2：创建并发保护包装类
```powershell
# 创建新文件
New-Item -Path "src/YLproxy.Core/Concurrency/FileLock.cs" -ItemType File
```

**文件内容：**
```csharp
using System.IO;
using System.Threading;

namespace YLproxy.Core.Concurrency;

/// <summary>
/// 提供文件级别的读写锁，防止并发访问冲突
/// </summary>
public class FileLock : IDisposable
{
    private readonly string _filePath;
    private readonly FileStream _lockStream;
    private static readonly Dictionary<string, int> _lockCounters = new();
    private static readonly object _lockSync = new();

    public FileLock(string filePath, FileAccess access = FileAccess.ReadWrite)
    {
        _filePath = filePath;
        
        lock (_lockSync)
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 创建锁文件
            var lockFilePath = filePath + ".lock";
            _lockStream = new FileStream(
                lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            
            _lockCounters[filePath] = _lockCounters.GetValueOrDefault(filePath, 0) + 1;
        }
    }

    public void Dispose()
    {
        _lockStream?.Dispose();
        
        lock (_lockSync)
        {
            _lockCounters[_filePath]--;
            if (_lockCounters[_filePath] <= 0)
            {
                var lockFilePath = _filePath + ".lock";
                if (File.Exists(lockFilePath))
                {
                    try { File.Delete(lockFilePath); } catch { }
                }
                _lockCounters.Remove(_filePath);
            }
        }
    }
}
```

#### 步骤 1.2.3：修改 ProxyDataService 使用文件锁
```csharp
// 在 src/YLproxy.Core/ProxyDataService.cs 中
// 修改 Load 方法：
public ProxyConfig Load()
{
    using var fileLock = new FileLock(_configPath, FileAccess.Read);
    var json = File.ReadAllText(_configPath);
    return JsonSerializer.Deserialize<ProxyConfig>(json) ?? new ProxyConfig();
}

// 修改 Save 方法：
public void Save(ProxyConfig config)
{
    using var fileLock = new FileLock(_configPath, FileAccess.Write);
    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
    { 
        WriteIndented = true 
    });
    
    // 先写入临时文件，确保原子性
    var tempPath = _configPath + ".tmp";
    File.WriteAllText(tempPath, json);
    
    // 替换原文件
    if (File.Exists(_configPath))
    {
        File.Replace(tempPath, _configPath, null);
    }
    else
    {
        File.Move(tempPath, _configPath);
    }
}
```

#### 步骤 1.2.4：添加并发测试
```powershell
# 创建并发测试文件
New-Item -Path "tests/ConcurrencyTests.cs" -ItemType File
```

**测试内容：**
```csharp
using Xunit;
using System.Threading.Tasks;
using System.Collections.Generic;
using YLproxy.Core;

namespace YLproxy.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task ProxyDataService_ConcurrentWrite_ShouldNotCorruptData()
    {
        // Arrange
        var configPath = "test_concurrent_config.json";
        var service = new ProxyDataService(configPath, skipPathValidation: true);
        
        var tasks = new List<Task>();
        
        // Act - 并发写入
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var config = service.Load();
                config.Proxies.Add(new ProxyItem 
                { 
                    Id = i, 
                    Name = $"Proxy-{i}",
                    RemoteHost = "test.com",
                    RemotePort = 8080,
                    LocalPort = 9000 + i,
                    Status = ProxyStatus.Stopped
                });
                service.Save(config);
            }));
        }
        
        await Task.WhenAll(tasks);
        
        // Assert - 验证数据完整性
        var finalConfig = service.Load();
        Assert.Equal(10, finalConfig.Proxies.Count);
        
        // Cleanup
        File.Delete(configPath);
    }
}
```

#### 步骤 1.2.5：验证和提交
```powershell
# 编译
dotnet build YLproxy.sln

# 运行并发测试
dotnet test tests/YLproxy.Tests.csproj --filter "FullyQualifiedName~Concurrency"

# 运行全部测试
dotnet test tests/YLproxy.Tests.csproj

# 提交
git add src/ tests/
git commit -m "[Phase 1.2] 数据持久化并发保护 - 实现文件锁机制防止并发冲突"
git push origin phase1-stability-enhancement
```

---

## 子阶段 1.3：文件系统监控线程安全（70% → 72%）

### 目标
- 定位 FileSystemWatcher 使用位置
- 引入 ReaderWriterLockSlim 保护共享资源
- 添加线程安全测试

### 执行步骤

#### 步骤 1.3.1：搜索 FileSystemWatcher 使用
```powershell
# 搜索 FileSystemWatcher
Select-String -Path "src\**\*.cs" -Pattern "FileSystemWatcher" -AllMatches
```

**预期位置：** 可能在配置监控或日志监控中使用

#### 步骤 1.3.2：分析现有实现
```powershell
# 查看具体实现文件
# 假设在 src/YLproxy.Core/ConfigWatcher.cs 或类似文件中
```

#### 步骤 1.3.3：实现线程安全包装
```powershell
# 创建线程安全辅助类
New-Item -Path "src/YLproxy.Core/Concurrency/ThreadSafeCollection.cs" -ItemType File
```

**文件内容：**
```csharp
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace YLproxy.Core.Concurrency;

/// <summary>
/// 线程安全的集合包装器
/// </summary>
public class ThreadSafeCollection<T>
{
    private readonly List<T> _items = new();
    private readonly ReaderWriterLockSlim _lock = new();
    
    public void Add(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            _items.Add(item);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    public void Remove(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            _items.Remove(item);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    public List<T> GetAll()
    {
        _lock.EnterReadLock();
        try
        {
            return new List<T>(_items);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
    
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _items.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}
```

#### 步骤 1.3.4：修改 FileSystemWatcher 事件处理
```csharp
// 在使用 FileSystemWatcher 的文件中
// 添加锁保护

private readonly ReaderWriterLockSlim _eventLock = new ReaderWriterLockSlim();

private void OnChanged(object sender, FileSystemEventArgs e)
{
    _eventLock.EnterWriteLock();
    try
    {
        // 处理文件变更
        ProcessFileChange(e);
    }
    finally
    {
        _eventLock.ExitWriteLock();
    }
}
```

#### 步骤 1.3.5：添加线程安全测试
```powershell
# 创建测试文件
New-Item -Path "tests/FileSystemWatcherTests.cs" -ItemType File
```

#### 步骤 1.3.6：验证和提交
```powershell
dotnet build YLproxy.sln
dotnet test tests/YLproxy.Tests.csproj

git add src/ tests/
git commit -m "[Phase 1.3] 文件系统监控线程安全 - 引入ReaderWriterLockSlim保护共享资源"
git push origin phase1-stability-enhancement
```

---

## 子阶段 1.4：配置迁移工具完善（72% → 75%）

### 目标
- 完善 DPAPI 迁移脚本的回滚机制
- 添加配置版本兼容性检查
- 实现自动备份功能

### 执行步骤

#### 步骤 1.4.1：检查现有迁移脚本
```powershell
# 查看现有迁移脚本
Get-Content scripts\migrate-proxy-data.ps1
```

#### 步骤 1.4.2：增强迁移脚本
```powershell
# 编辑 scripts/migrate-proxy-data.ps1
# 添加以下功能：
```

**增强功能清单：**
1. **自动备份功能**
```powershell
function Backup-Config {
    param([string]$ConfigPath)
    
    $backupDir = "data\backups"
    if (-not (Test-Path $backupDir)) {
        New-Item -ItemType Directory -Path $backupDir | Out-Null
    }
    
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupPath = Join-Path $backupDir "config_backup_$timestamp.json"
    Copy-Item $ConfigPath $backupPath
    
    Write-Host "Backup created: $backupPath" -ForegroundColor Green
    return $backupPath
}
```

2. **回滚机制**
```powershell
function Restore-FromBackup {
    param([string]$BackupPath, [string]$ConfigPath)
    
    if (Test-Path $BackupPath) {
        Copy-Item $BackupPath $ConfigPath -Force
        Write-Host "Restored from backup: $BackupPath" -ForegroundColor Yellow
        return $true
    }
    
    Write-Host "Backup not found: $BackupPath" -ForegroundColor Red
    return $false
}
```

3. **版本兼容性检查**
```powershell
function Test-ConfigVersion {
    param([string]$ConfigPath)
    
    if (-not (Test-Path $ConfigPath)) {
        return "new"
    }
    
    $config = Get-Content $ConfigPath | ConvertFrom-Json
    
    if ($config.PSObject.Properties.Name -contains "Version") {
        return $config.Version
    }
    
    return "legacy"
}
```

#### 步骤 1.4.3：添加配置版本字段到模型
```csharp
// 在 src/YLproxy.Models/Config/ProxyConfig.cs 中
public class ProxyConfig
{
    public const string CurrentVersion = "1.1";
    
    public string Version { get; set; } = CurrentVersion;
    public List<ProxyItem> Proxies { get; set; } = new();
}
```

#### 步骤 1.4.4：实现自动版本升级
```csharp
// 在 ProxyDataService.cs 中添加
private void UpgradeConfigIfNeeded(ProxyConfig config)
{
    if (string.IsNullOrEmpty(config.Version))
    {
        _logger.Info("Detected legacy config format, upgrading to version 1.0");
        config.Version = "1.0";
    }
    
    if (config.Version == "1.0")
    {
        _logger.Info("Upgrading config from version 1.0 to 1.1");
        // 执行版本 1.0 到 1.1 的升级逻辑
        config.Version = "1.1";
    }
    
    // 保存升级后的配置
    Save(config);
}
```

#### 步骤 1.4.5：添加迁移测试
```powershell
# 创建测试文件
New-Item -Path "tests/ConfigMigrationTests.cs" -ItemType File
```

**测试内容：**
```csharp
using Xunit;
using YLproxy.Core;
using System.IO;

namespace YLproxy.Tests;

public class ConfigMigrationTests
{
    [Fact]
    public void ProxyDataService_LegacyConfig_ShouldAutoUpgrade()
    {
        // Arrange
        var testConfigPath = "test_legacy_config.json";
        var legacyJson = @"{""Proxies"":[]}";
        File.WriteAllText(testConfigPath, legacyJson);
        
        // Act
        var service = new ProxyDataService(testConfigPath, skipPathValidation: true);
        var config = service.Load();
        
        // Assert
        Assert.Equal("1.1", config.Version);
        
        // Cleanup
        File.Delete(testConfigPath);
    }
}
```

#### 步骤 1.4.6：验证和提交
```powershell
# 测试迁移脚本
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\migrate-proxy-data.ps1 -WhatIf

# 编译项目
dotnet build YLproxy.sln

# 运行测试
dotnet test tests/YLproxy.Tests.csproj --filter "FullyQualifiedName~Migration"

# 提交
git add scripts/ src/ tests/
git commit -m "[Phase 1.4] 配置迁移工具完善 - 添加回滚机制、版本检查和自动备份"
git push origin phase1-stability-enhancement
```

---

## 阶段一完成验证

### 最终验证步骤
```powershell
# 1. 完整编译
dotnet build YLproxy.sln -c Release

# 2. 运行所有测试
dotnet test tests/YLproxy.Tests.csproj

# 3. 检查代码质量
dotnet format YLproxy.sln --verify-no-changes

# 4. 运行 GUI 应用测试
dotnet run --project src/YLproxy.GUI

# 5. 手动测试关键功能
# - 添加代理
# - 测试代理
# - 启动代理
# - 验证日志记录
```

### 创建合并请求
```powershell
# 切换到 main 分支
git checkout main

# 合并功能分支
git merge phase1-stability-enhancement

# 推送到远程
git push origin main

# 删除功能分支
git branch -d phase1-stability-enhancement
git push origin --delete phase1-stability-enhancement
```

### 更新进度文档
```powershell
# 更新 docs/progress.md
# 添加阶段一完成记录
# 更新总体进度为 75%
```

---

## 执行记录模板

每个子阶段完成后填写：

```markdown
## [子阶段名称] 执行记录

**执行时间：** YYYY-MM-DD
**执行环境：** Windows本地
**执行人：** 本地AI

### 执行内容
- [ ] 任务1
- [ ] 任务2
- [ ] 任务3

### 遇到问题
- 问题描述1 → 解决方案
- 问题描述2 → 解决方案

### 同步记录
- Commit: [hash] [message]
- CI 状态: ✅ / ❌
- PR: #[number]

### 进度更新
- 前进度：X%
- 后进度：Y%
- 增量：+Z%
```

---

## 注意事项

1. **每个子阶段完成后立即提交**，不要堆积大量变更
2. **提交信息格式**：`[Phase 1.X] 简短描述`
3. **保持 CI 始终通过**，不允许失败提交
4. **遇到问题及时记录**，在执行记录中说明
5. **测试覆盖率不能下降**，新增代码必须有测试
6. **文档与代码同步更新**，保持一致性

---

*执行方案版本：1.0 | 创建时间：2026-07-26*
