using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Proxy;

public sealed class ProxyProcessManager
{
    // key: Proxy.Id
    private static readonly ConcurrentDictionary<int, Process> Processes = new();

    private static string GetRuntime3ProxyPath()
    {
        // 使用PathResolver统一解析路径，避免硬编码和路径计算错误
        return PathResolver.ResolvePath("runtime", "3proxy");
    }

    private static string Runtime3ProxyPath
        => GetRuntime3ProxyPath();

    /// <summary>
    /// 验证所有必要的3proxy依赖文件是否存在
    /// </summary>
    /// <exception cref="FileNotFoundException">当任何必要依赖缺失时抛出</exception>
    public static void Ensure3ProxyDependencies()
    {
        var exePath = Get3ProxyExePath();
        
        Console.WriteLine($"[ProxyProcessManager] Checking 3proxy dependencies...");
        Console.WriteLine($"[ProxyProcessManager] Checking main executable: {exePath}");

        // 检查主执行文件
        if (!File.Exists(exePath))
        {
            string errorMsg = $"3proxy.exe not found at {exePath}. " +
                            $"Please ensure 3proxy is properly installed. " +
                            $"Expected location: {Path.GetDirectoryName(exePath)}";
            Console.WriteLine($"[ProxyProcessManager] ERROR: {errorMsg}");
            throw new FileNotFoundException(errorMsg);
        }
        
        Console.WriteLine($"[ProxyProcessManager] Main executable found: {exePath}");

        // 检查必要的DLL依赖
        var requiredDlls = new[] { "FilePlugin.dll", "StringsPlugin.dll" };
        var dllDirectory = Path.GetDirectoryName(exePath);
        
        Console.WriteLine($"[ProxyProcessManager] Checking DLL dependencies in: {dllDirectory}");

        foreach (var dll in requiredDlls)
        {
            var dllPath = Path.Combine(dllDirectory, dll);
            Console.WriteLine($"[ProxyProcessManager] Checking dependency: {dllPath}");
            
            if (!File.Exists(dllPath))
            {
                string errorMsg = $"Required dependency {dll} not found at {dllPath}. " +
                                $"Please ensure all 3proxy dependencies are present.";
                Console.WriteLine($"[ProxyProcessManager] ERROR: {errorMsg}");
                throw new FileNotFoundException(errorMsg);
            }
            
            Console.WriteLine($"[ProxyProcessManager] Dependency found: {dllPath}");
        }

        // 确保配置和日志目录存在
        var configDir = Get3ProxyConfigDirectory();
        var logDir = Get3ProxyLogDirectory();
        
        Console.WriteLine($"[ProxyProcessManager] Ensuring config directory exists: {configDir}");
        Console.WriteLine($"[ProxyProcessManager] Ensuring log directory exists: {logDir}");
        
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(logDir);
        
        Console.WriteLine($"[ProxyProcessManager] All 3proxy dependencies verified successfully.");
    }

    /// <summary>
    /// 获取3proxy可执行文件的完整路径
    /// </summary>
    private static string Get3ProxyExePath()
    {
        string? runtimePath = Get3ProxyDirectory();
        if (runtimePath == null)
        {
            throw new InvalidOperationException("Unable to determine 3proxy runtime directory");
        }
        
        string exePath = Path.Combine(runtimePath, "bin64", "3proxy.exe");
        Console.WriteLine($"[ProxyProcessManager] Resolved 3proxy.exe path: {exePath}");
        return exePath;
    }

    /// <summary>
    /// 获取3proxy目录的完整路径
    /// </summary>
    private static string Get3ProxyDirectory()
    {
        string? path = GetRuntime3ProxyPath();
        if (path == null)
        {
            throw new InvalidOperationException("Unable to determine 3proxy runtime directory");
        }
        return path;
    }

    /// <summary>
    /// 获取3proxy配置目录的完整路径
    /// </summary>
    private static string Get3ProxyConfigDirectory()
    {
        string? proxyDir = Get3ProxyDirectory();
        if (proxyDir == null)
        {
            throw new InvalidOperationException("Unable to determine 3proxy directory");
        }
        return Path.Combine(proxyDir, "cfg");
    }

    /// <summary>
    /// 获取3proxy日志目录的完整路径
    /// </summary>
    private static string Get3ProxyLogDirectory()
    {
        string? proxyDir = Get3ProxyDirectory();
        if (proxyDir == null)
        {
            throw new InvalidOperationException("Unable to determine 3proxy directory");
        }
        return Path.Combine(proxyDir, "logs");
    }

    public static void Start(ProxyItem proxy)
    {
        if (proxy is null) throw new ArgumentNullException(nameof(proxy));

        Console.WriteLine($"[ProxyProcessManager] Starting proxy ID: {proxy.Id}");
        
        // 添加依赖验证
        Ensure3ProxyDependencies();

        // Prevent double-start
        if (Processes.TryGetValue(proxy.Id, out var existing))
        {
            if (!existing.HasExited)
            {
                Console.WriteLine($"[ProxyProcessManager] Proxy ID {proxy.Id} is already running.");
                return;
            }
        }

        var cfgText = ConfigGenerator.Generate(proxy);
        var cfgPath = Path.Combine(Get3ProxyConfigDirectory(), $"{proxy.Id}.cfg");
        Console.WriteLine($"[ProxyProcessManager] Writing config to: {cfgPath}");
        File.WriteAllText(cfgPath, cfgText);

        // Start 3proxy in its working directory so relative cfg path works.
        var exePath = Get3ProxyExePath();
        if (!File.Exists(exePath))
            throw new FileNotFoundException($"3proxy.exe not found: {exePath}");

        Console.WriteLine($"[ProxyProcessManager] Starting 3proxy with arguments: cfg\\{proxy.Id}.cfg");
        Console.WriteLine($"[ProxyProcessManager] Working directory: {Get3ProxyDirectory()}");

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"cfg\\{proxy.Id}.cfg",
            WorkingDirectory = Get3ProxyDirectory(),
            CreateNoWindow = true,
            UseShellExecute = false
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        if (!process.Start())
            throw new InvalidOperationException("Failed to start 3proxy process");

        Console.WriteLine($"[ProxyProcessManager] 3proxy started successfully with PID: {process.Id}");
        Processes[proxy.Id] = process;
    }

    public static bool IsRunning(ProxyItem proxy)
    {
        if (proxy is null) return false;

        // 添加依赖验证（虽然检查运行状态不严格需要exe，但保持一致性）
        try
        {
            Ensure3ProxyDependencies();
        }
        catch
        {
            // 如果依赖检查失败，认为进程不在运行中（安全策略）
            Console.WriteLine($"[ProxyProcessManager] Dependency check failed for proxy {proxy?.Id ?? 0}, considering as not running.");
            return false;
        }

        if (!Processes.TryGetValue(proxy.Id, out var process))
        {
            Console.WriteLine($"[ProxyProcessManager] No process found for proxy ID: {proxy.Id}");
            return false;
        }

        bool isRunning = !process.HasExited;
        Console.WriteLine($"[ProxyProcessManager] Proxy ID {proxy.Id} is running: {isRunning} (HasExited: {process.HasExited})");
        return isRunning;
    }

    public static void Stop(ProxyItem proxy)
    {
        if (proxy is null) throw new ArgumentNullException(nameof(proxy));

        Console.WriteLine($"[ProxyProcessManager] Stopping proxy ID: {proxy.Id}");

        if (!Processes.TryGetValue(proxy.Id, out var process))
        {
            Console.WriteLine($"[ProxyProcessManager] No process found to stop for proxy ID: {proxy.Id}");
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                Console.WriteLine($"[ProxyProcessManager] Killing 3proxy process with PID: {process.Id}");
                // 3proxy doesn't provide an RPC in this integration; kill is the reliable option.
                process.Kill(true);

                // small wait to release port
                Console.WriteLine($"[ProxyProcessManager] Waiting for process to exit...");
                for (var i = 0; i < 30 && !process.HasExited; i++)
                {
                    Thread.Sleep(50);
                }
                
                if (process.HasExited)
                {
                    Console.WriteLine($"[ProxyProcessManager] 3proxy process exited successfully.");
                }
                else
                {
                    Console.WriteLine($"[ProxyProcessManager] Warning: 3proxy process did not exit after waiting.");
                }
            }
            else
            {
                Console.WriteLine($"[ProxyProcessManager] Process already exited for proxy ID: {proxy.Id}");
            }
        }
        finally
        {
            Processes.TryRemove(proxy.Id, out _);
            Console.WriteLine($"[ProxyProcessManager] Removed proxy ID {proxy.Id} from process tracking.");
        }
    }
}