using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Proxy;

public sealed class ProxyProcessManager
{
    // key: Proxy.Id
    private static readonly ConcurrentDictionary<int, Process> Processes = new();

    private static readonly ILogger _logger = LoggerFactory.CreateLogger();

    public static void Configure(ThreeProxyConfig settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ProxyRuntimeConfiguration.Configure(settings.RuntimeDirectory, settings.RequiredDlls);
    }

    private static string GetRuntime3ProxyPath()
    {
        return ProxyRuntimeConfiguration.GetRuntimeDirectory();
    }

    private static string Runtime3ProxyPath
        => GetRuntime3ProxyPath();

    private static string GetConfigPath(ProxyItem proxy)
    {
        return Path.Combine(GetRuntime3ProxyPath(), "cfg", $"{proxy.Id}.cfg");
    }

    /// <summary>
    /// 验证所有必要的3proxy依赖文件是否存在
    /// </summary>
    /// <exception cref="FileNotFoundException">当任何必要依赖缺失时抛出</exception>
    public static void Ensure3ProxyDependencies()
    {
        var exePath = Get3ProxyExePath();

        _logger.Debug($"Checking 3proxy dependencies...");
        _logger.Debug($"Checking main executable: {exePath}");

        // 检查主执行文件
        if (!File.Exists(exePath))
        {
            string errorMsg = $"3proxy.exe not found at {exePath}. " +
                            $"Please ensure 3proxy is properly installed. " +
                            $"Expected location: {Path.GetDirectoryName(exePath)}";
            _logger.Error(errorMsg);
            throw new FileNotFoundException(errorMsg);
        }

        _logger.Debug($"Main executable found: {exePath}");

        // 检查必要的DLL依赖
        var dllDirectory = Path.GetDirectoryName(exePath)
            ?? throw new InvalidOperationException($"Unable to determine DLL directory for {exePath}");

        _logger.Debug($"Checking DLL dependencies in: {dllDirectory}");

        foreach (var dll in ProxyRuntimeConfiguration.GetRequiredDlls())
        {
            var dllPath = Path.Combine(dllDirectory, dll);
            _logger.Debug($"Checking dependency: {dllPath}");

            if (!File.Exists(dllPath))
            {
                string errorMsg = $"Required dependency {dll} not found at {dllPath}. " +
                                $"Please ensure all 3proxy dependencies are present.";
                _logger.Error(errorMsg);
                throw new FileNotFoundException(errorMsg);
            }

            _logger.Debug($"Dependency found: {dllPath}");
        }

        // 确保配置和日志目录存在
        var configDir = Get3ProxyConfigDirectory();
        var logDir = Get3ProxyLogDirectory();

        _logger.Debug($"Ensuring config directory exists: {configDir}");
        _logger.Debug($"Ensuring log directory exists: {logDir}");

        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(logDir);

        _logger.Debug($"All 3proxy dependencies verified successfully.");
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
        _logger.Debug($"Resolved 3proxy.exe path: {exePath}");
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

        _logger.Debug($"Starting proxy ID: {proxy.Id}");

        // 添加依赖验证
        Ensure3ProxyDependencies();

        // Prevent double-start
        if (Processes.TryGetValue(proxy.Id, out var existing))
        {
            if (!existing.HasExited)
            {
                _logger.Debug($"Proxy ID {proxy.Id} is already running.");
                return;
            }

            if (Processes.TryRemove(proxy.Id, out var exitedProcess))
            {
                exitedProcess.Dispose();
                TryDeleteConfigFile(GetConfigPath(proxy));
            }
        }

        if (!IsPortAvailable(proxy.LocalPort))
            throw new InvalidOperationException($"Local proxy port {proxy.LocalPort} is already in use.");

        var cfgPath = GetConfigPath(proxy);
        Process? process = null;

        try
        {
            var cfgText = ConfigGenerator.Generate(proxy);
            _logger.Debug($"Writing config to: {cfgPath}");
            File.WriteAllText(cfgPath, cfgText);

            // Start 3proxy in its working directory so relative cfg path works.
            var exePath = Get3ProxyExePath();
            if (!File.Exists(exePath))
                throw new FileNotFoundException($"3proxy.exe not found: {exePath}");

            _logger.Debug($"Starting 3proxy with arguments: cfg\\{proxy.Id}.cfg");
            _logger.Debug($"Working directory: {Get3ProxyDirectory()}");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"cfg\\{proxy.Id}.cfg",
                WorkingDirectory = Get3ProxyDirectory(),
                CreateNoWindow = true,
                UseShellExecute = false
            };

            process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            if (!process.Start())
                throw new InvalidOperationException("Failed to start 3proxy process");

            _logger.Debug($"3proxy started successfully with PID: {process.Id}");
            Processes[proxy.Id] = process;
            WaitForPort(process, proxy.LocalPort, TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to start proxy ID {proxy.Id}: {ex.Message}", ex);

            try
            {
                if (process is not null && !process.HasExited)
                    process.Kill(true);
            }
            catch (Exception killEx)
            {
                _logger.Warn($"Failed to kill process during cleanup: {killEx.Message}");
            }

            process?.Dispose();
            Processes.TryRemove(proxy.Id, out _);
            TryDeleteConfigFile(cfgPath);
            throw;
        }
    }

    public static bool IsRunning(ProxyItem proxy)
    {
        if (proxy is null) return false;
        var proxyId = proxy.Id;
        var configPath = GetConfigPath(proxy);

        // 添加依赖验证（虽然检查运行状态不严格需要exe，但保持一致性）
        try
        {
            Ensure3ProxyDependencies();
        }
        catch (Exception ex)
        {
            // 如果依赖检查失败，认为进程不在运行中（安全策略）
            _logger.Warn($"Dependency check failed for proxy {proxy?.Id ?? 0}, considering as not running: {ex.Message}");
            if (Processes.TryRemove(proxyId, out var failedProcess))
                failedProcess?.Dispose();
            TryDeleteConfigFile(configPath);
            return false;
        }

        if (!Processes.TryGetValue(proxy.Id, out var process))
        {
            _logger.Debug($"No process found for proxy ID: {proxy.Id}");
            return false;
        }

        bool isRunning = !process.HasExited;
        _logger.Debug($"Proxy ID {proxy.Id} is running: {isRunning} (HasExited: {process.HasExited})");

        if (!isRunning && Processes.TryRemove(proxy.Id, out var exitedProcess))
        {
            exitedProcess.Dispose();
            TryDeleteConfigFile(configPath);
        }

        return isRunning;
    }

    public static void Stop(ProxyItem proxy)
    {
        if (proxy is null) throw new ArgumentNullException(nameof(proxy));

        _logger.Debug($"Stopping proxy ID: {proxy.Id}");

        if (!Processes.TryGetValue(proxy.Id, out var process))
        {
            _logger.Debug($"No process found to stop for proxy ID: {proxy.Id}");
            TryDeleteConfigFile(GetConfigPath(proxy));
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                _logger.Debug($"Killing 3proxy process with PID: {process.Id}");
                // 3proxy doesn't provide an RPC in this integration; kill is the reliable option.
                process.Kill(true);

                // small wait to release port
                _logger.Debug($"Waiting for process to exit...");
                for (var i = 0; i < 30 && !process.HasExited; i++)
                {
                    Thread.Sleep(50);
                }

                if (process.HasExited)
                {
                    _logger.Debug($"3proxy process exited successfully.");
                }
                else
                {
                    _logger.Warn($"3proxy process did not exit after waiting.");
                }
            }
            else
            {
                _logger.Debug($"Process already exited for proxy ID: {proxy.Id}");
            }
        }
        finally
        {
            Processes.TryRemove(proxy.Id, out _);
            TryDeleteConfigFile(GetConfigPath(proxy));
            process.Dispose();
            _logger.Debug($"Removed proxy ID {proxy.Id} from process tracking.");
        }
    }

    private static void TryDeleteConfigFile(string configPath)
    {
        try
        {
            if (File.Exists(configPath))
                File.Delete(configPath);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Unable to delete runtime config: {ex.Message}");
        }
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static void WaitForPort(Process process, int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            process.Refresh();
            if (process.HasExited)
                throw new InvalidOperationException($"3proxy exited during startup with code {process.ExitCode}.");

            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(IPAddress.Loopback, port);
                if (connectTask.Wait(TimeSpan.FromMilliseconds(250)) && connectTask.IsCompletedSuccessfully)
                    return;
            }
            catch (SocketException)
            {
            }
            catch (AggregateException ex) when (ex.InnerException is SocketException)
            {
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"3proxy did not listen on local port {port} within {timeout}.");
    }
}
