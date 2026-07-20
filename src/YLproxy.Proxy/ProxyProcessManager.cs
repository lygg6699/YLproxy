using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Models.Config;
using YLproxy.Utils;

namespace YLproxy.Proxy;

public sealed class ProxyProcessManager
{
    // key: Proxy.Id
    private readonly ConcurrentDictionary<int, Process> _processes = new();
    private readonly ConcurrentDictionary<int, ManagedProxyForwarder> _forwarders = new();
    private readonly ProxyRuntimeConfiguration _runtimeConfig;
    private readonly ILogger _logger;

    /// <summary>
    /// 默认全局实例，用于向后兼容静态调用。
    /// </summary>
    private static readonly ProxyProcessManager _defaultInstance = new();

    /// <summary>
    /// 获取默认全局实例（仅用于向后兼容的过渡期）。
    /// </summary>
    public static ProxyProcessManager Default => _defaultInstance;

    public ProxyProcessManager()
        : this(new ProxyRuntimeConfiguration(), LoggerFactory.CreateLogger())
    {
    }

    public ProxyProcessManager(ProxyRuntimeConfiguration runtimeConfig, ILogger? logger = null)
    {
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        _logger = logger ?? LoggerFactory.CreateLogger();
    }

    public void Configure(ThreeProxyConfig settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _runtimeConfig.Configure(settings.RuntimeDirectory, settings.RequiredDlls);
    }

    private string GetRuntime3ProxyPath()
    {
        return _runtimeConfig.GetRuntimeDirectory();
    }

    private string Runtime3ProxyPath
        => GetRuntime3ProxyPath();

    private string GetConfigPath(ProxyItem proxy)
    {
        return Path.Combine(GetRuntime3ProxyPath(), "cfg", $"{proxy.Id}.cfg");
    }

    /// <summary>
    /// 验证所有必要的3proxy依赖文件是否存在
    /// </summary>
    /// <exception cref="FileNotFoundException">当任何必要依赖缺失时抛出</exception>
    public void Ensure3ProxyDependencies()
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
        var dllDirectory = Path.GetDirectoryName(exePath);
        ArgumentNullException.ThrowIfNull(dllDirectory);

        _logger.Debug($"Checking DLL dependencies in: {dllDirectory}");

        foreach (var dll in _runtimeConfig.GetRequiredDlls())
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

        // 清理孤立 cfg 文件（进程已不再运行的残留配置）
        CleanOrphanedConfigFiles(configDir);

        _logger.Debug($"All 3proxy dependencies verified successfully.");
    }

    /// <summary>
    /// 清理 cfg 目录中不再有对应运行进程的孤立配置文件，避免明文凭据残留。
    /// </summary>
    private void CleanOrphanedConfigFiles(string configDir)
    {
        try
        {
            var cfgFiles = Directory.GetFiles(configDir, "*.cfg");
            foreach (var cfgFile in cfgFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(cfgFile);
                if (!int.TryParse(fileName, out var proxyId))
                    continue;

                // 如果有活动的进程跟踪记录且进程仍在运行，保留 cfg
                if (_processes.TryGetValue(proxyId, out var process) && !process.HasExited)
                    continue;

                TryDeleteConfigFile(cfgFile);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Orphaned cfg cleanup scan failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取3proxy可执行文件的完整路径
    /// </summary>
    private string Get3ProxyExePath()
    {
        var runtimePath = Get3ProxyDirectory();
        ArgumentNullException.ThrowIfNull(runtimePath);

        var exePath = Path.Combine(runtimePath, "bin64", "3proxy.exe");
        _logger.Debug($"Resolved 3proxy.exe path: {exePath}");
        return exePath;
    }

    /// <summary>
    /// 获取3proxy目录的完整路径
    /// </summary>
    private string Get3ProxyDirectory()
    {
        var runtimePath = GetRuntime3ProxyPath();
        ArgumentNullException.ThrowIfNull(runtimePath);
        return runtimePath;
    }

    /// <summary>
    /// 获取3proxy配置目录的完整路径
    /// </summary>
    private string Get3ProxyConfigDirectory()
    {
        var proxyDir = Get3ProxyDirectory();
        return Path.Combine(proxyDir, "cfg");
    }

    /// <summary>
    /// 获取3proxy日志目录的完整路径
    /// </summary>
    private string Get3ProxyLogDirectory()
    {
        var proxyDir = Get3ProxyDirectory();
        return Path.Combine(proxyDir, "logs");
    }

    public void Start(ProxyItem proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        _logger.Debug($"Starting proxy ID: {proxy.Id}");

        // Prevent double-start
        if (_processes.TryGetValue(proxy.Id, out var existing))
        {
            if (!existing.HasExited)
            {
                _logger.Debug($"Proxy ID {proxy.Id} is already running.");
                return;
            }

            if (_processes.TryRemove(proxy.Id, out var exitedProcess))
            {
                exitedProcess.Dispose();
                TryDeleteConfigFile(GetConfigPath(proxy));
            }
        }

        if (_forwarders.TryGetValue(proxy.Id, out var existingFwd))
        {
            if (existingFwd.IsListening)
            {
                _logger.Debug($"Proxy ID {proxy.Id} forwarder is already running.");
                return;
            }
            _forwarders.TryRemove(proxy.Id, out _);
            existingFwd.Dispose();
        }

        if (!IsPortAvailable(proxy.LocalPort))
            throw new InvalidOperationException($"Local proxy port {proxy.LocalPort} is already in use.");

        // 有凭据且上游非本地的代理优先使用 .NET ManagedProxyForwarder（正确处理 407 挑战-响应认证），
        // 无凭据或本地 mock 上游使用 3proxy。
        var isRemoteProxy = !string.IsNullOrEmpty(proxy.RemoteHost)
            && !proxy.RemoteHost.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && !proxy.RemoteHost.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        if (isRemoteProxy && !string.IsNullOrEmpty(proxy.Username) && !string.IsNullOrEmpty(proxy.Password))
        {
            try
            {
                var forwarder = new ManagedProxyForwarder(proxy, _logger);
                forwarder.Start();
                _forwarders[proxy.Id] = forwarder;
                _logger.Info($"Proxy ID {proxy.Id} started via ManagedProxyForwarder on port {proxy.LocalPort}");
                return;
            }
            catch (Exception ex)
            {
                _logger.Warn($"ManagedProxyForwarder failed for proxy ID {proxy.Id}, falling back to 3proxy: {ex.Message}");
            }
        }

        var cfgPath = GetConfigPath(proxy);
        Process? process = null;

        try
        {
            Ensure3ProxyDependencies();

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
            _processes[proxy.Id] = process;
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
            _processes.TryRemove(proxy.Id, out _);
            TryDeleteConfigFile(cfgPath);
            throw;
        }
    }

    public bool IsRunning(ProxyItem proxy)
    {
        if (proxy is null) return false;
        var proxyId = proxy.Id;
        var configPath = GetConfigPath(proxy);

        // 检查 ManagedProxyForwarder
        if (_forwarders.TryGetValue(proxy.Id, out var forwarder))
        {
            return forwarder.IsListening;
        }

        // 无 forwarder 时检查 3proxy 进程。
        try
        {
            Ensure3ProxyDependencies();
        }
        catch (Exception ex)
        {
            // 如果依赖检查失败，认为进程不在运行中（安全策略）
            _logger.Warn($"Dependency check failed for proxy {proxy?.Id ?? 0}, considering as not running: {ex.Message}");
            if (_processes.TryRemove(proxyId, out var failedProcess))
                failedProcess?.Dispose();
            TryDeleteConfigFile(configPath);
            return false;
        }

        if (!_processes.TryGetValue(proxy.Id, out var process))
        {
            _logger.Debug($"No process found for proxy ID: {proxy.Id}");
            return false;
        }

        bool isRunning = !process.HasExited;
        _logger.Debug($"Proxy ID {proxy.Id} is running: {isRunning} (HasExited: {process.HasExited})");

        if (!isRunning && _processes.TryRemove(proxy.Id, out var exitedProcess))
        {
            exitedProcess.Dispose();
            TryDeleteConfigFile(configPath);
        }

        return isRunning;
    }

    public void Stop(ProxyItem proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        _logger.Debug($"Stopping proxy ID: {proxy.Id}");

        // Stop ManagedProxyForwarder if it exists
        if (_forwarders.TryRemove(proxy.Id, out var forwarder))
        {
            forwarder.Stop();
            forwarder.Dispose();
            _logger.Info($"Proxy ID {proxy.Id} forwarder stopped.");
            return;
        }

        if (!_processes.TryGetValue(proxy.Id, out var process))
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
            _processes.TryRemove(proxy.Id, out _);
            TryDeleteConfigFile(GetConfigPath(proxy));
            process.Dispose();
            _logger.Debug($"Removed proxy ID {proxy.Id} from process tracking.");
        }
    }

    private void TryDeleteConfigFile(string configPath)
    {
        try
        {
            SimpleRetry.Execute(() =>
            {
                if (File.Exists(configPath))
                    File.Delete(configPath);
            }, maxAttempts: 3, delayMs: 50, logger: _logger);
        }
        catch (AggregateException ex)
        {
            _logger.Warn($"Unable to delete runtime config after retries: {ex.Message}");
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
                try
                {
                    client.Connect(IPAddress.Loopback, port);
                    return;
                }
                catch (SocketException) { }
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
