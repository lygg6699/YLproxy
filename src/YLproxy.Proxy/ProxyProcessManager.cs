using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Proxy;

public sealed class ProxyProcessManager
{
    private static readonly ConcurrentDictionary<int, Process> Processes = new();
    private static readonly ConcurrentDictionary<int, TransparentCoalescingForwarder> Forwarders = new();
    private static ILogger? _logger;

    internal static void AddForwarderForTesting(int proxyId, TransparentCoalescingForwarder forwarder)
    {
        Forwarders[proxyId] = forwarder;
    }

    internal static bool HasActiveForwarderForTesting(int proxyId)
    {
        return Forwarders.TryGetValue(proxyId, out var forwarder) && forwarder != null;
    }

    public static void Configure(ThreeProxyConfig settings, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _logger = logger;
        ProxyRuntimeConfiguration.Configure(settings.RuntimeDirectory, settings.RequiredDlls);
    }

    private static string GetRuntime3ProxyPath()
    {
        return ProxyRuntimeConfiguration.GetRuntimeDirectory();
    }

    private static string GetConfigPath(ProxyItem proxy)
    {
        return Path.Combine(GetRuntime3ProxyPath(), "cfg", $"{proxy.Id}.cfg");
    }

    public static void Ensure3ProxyDependencies()
    {
        var exePath = Get3ProxyExePath();

        LogInfo("Checking 3proxy dependencies...");
        LogInfo($"Checking main executable: {exePath}");

        if (!File.Exists(exePath))
        {
            string errorMsg = $"3proxy.exe not found at {exePath}. " +
                            "Please ensure 3proxy is properly installed. " +
                            $"Expected location: {Path.GetDirectoryName(exePath)}";
            LogError(errorMsg);
            throw new FileNotFoundException(errorMsg);
        }

        LogInfo($"Main executable found: {exePath}");

        var dllDirectory = Path.GetDirectoryName(exePath)
            ?? throw new InvalidOperationException($"Unable to determine DLL directory for {exePath}");

        LogInfo($"Checking DLL dependencies in: {dllDirectory}");

        foreach (var dll in ProxyRuntimeConfiguration.GetRequiredDlls())
        {
            var dllPath = Path.Combine(dllDirectory, dll);
            LogInfo($"Checking dependency: {dllPath}");

            if (!File.Exists(dllPath))
            {
                string errorMsg = $"Required dependency {dll} not found at {dllPath}. " +
                                "Please ensure all 3proxy dependencies are present.";
                LogError(errorMsg);
                throw new FileNotFoundException(errorMsg);
            }

            LogInfo($"Dependency found: {dllPath}");
        }

        var configDir = Get3ProxyConfigDirectory();
        var logDir = Get3ProxyLogDirectory();

        LogInfo($"Ensuring config directory exists: {configDir}");
        LogInfo($"Ensuring log directory exists: {logDir}");

        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(logDir);

        LogInfo("All 3proxy dependencies verified successfully.");
    }

    private static string Get3ProxyExePath()
    {
        string? runtimePath = Get3ProxyDirectory();
        if (runtimePath == null)
        {
            throw new InvalidOperationException("Unable to determine 3proxy runtime directory");
        }

        string exePath = Path.Combine(runtimePath, "bin64", "3proxy.exe");
        LogInfo($"Resolved 3proxy.exe path: {exePath}");
        return exePath;
    }

    private static string Get3ProxyDirectory()
    {
        string? path = GetRuntime3ProxyPath();
        if (path == null)
        {
            throw new InvalidOperationException("Unable to determine 3proxy runtime directory");
        }
        return path;
    }

    private static string Get3ProxyConfigDirectory()
    {
        string? proxyDir = Get3ProxyDirectory();
        if (proxyDir == null)
        {
            throw new InvalidOperationException("Unable to determine 3proxy directory");
        }
        return Path.Combine(proxyDir, "cfg");
    }

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
        ArgumentNullException.ThrowIfNull(proxy);

        LogInfo($"Starting proxy ID: {proxy.Id}");

        Ensure3ProxyDependencies();

        if (Processes.TryGetValue(proxy.Id, out var existing))
        {
            if (!existing.HasExited)
            {
                LogInfo($"Proxy ID {proxy.Id} is already running.");
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
            LogInfo($"Writing config to: {cfgPath}");
            File.WriteAllText(cfgPath, cfgText);

            var exePath = Get3ProxyExePath();
            if (!File.Exists(exePath))
                throw new FileNotFoundException($"3proxy.exe not found: {exePath}");

            LogInfo($"Starting 3proxy with arguments: cfg\\{proxy.Id}.cfg");
            LogInfo($"Working directory: {Get3ProxyDirectory()}");

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

            LogInfo($"3proxy started successfully with PID: {process.Id}");
            Processes[proxy.Id] = process;
            WaitForPort(process, proxy.LocalPort, TimeSpan.FromSeconds(5));
        }
        catch
        {
            try
            {
                if (process is not null && !process.HasExited)
                    process.Kill(true);
            }
            catch
            {
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

        try
        {
            Ensure3ProxyDependencies();
        }
        catch
        {
            LogWarn($"Dependency check failed for proxy {proxyId}, considering as not running.");
            if (Processes.TryRemove(proxyId, out var failedProcess))
                failedProcess?.Dispose();
            TryDeleteConfigFile(configPath);
            return false;
        }

        if (!Processes.TryGetValue(proxy.Id, out var process))
        {
            LogInfo($"No process found for proxy ID: {proxy.Id}");
            return false;
        }

        bool isRunning = !process.HasExited;
        LogInfo($"Proxy ID {proxy.Id} is running: {isRunning} (HasExited: {process.HasExited})");

        if (!isRunning && Processes.TryRemove(proxy.Id, out var exitedProcess))
        {
            exitedProcess.Dispose();
            TryDeleteConfigFile(configPath);
        }

        return isRunning;
    }

    public static void Stop(ProxyItem proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        LogInfo($"Stopping proxy ID: {proxy.Id}");

        if (Processes.TryGetValue(proxy.Id, out var process))
        {
            try
            {
                if (!process.HasExited)
                {
                    LogInfo($"Killing 3proxy process with PID: {process.Id}");
                    process.Kill(true);

                    LogInfo("Waiting for process to exit...");
                    for (var i = 0; i < 30 && !process.HasExited; i++)
                    {
                        Thread.Sleep(50);
                    }

                    if (process.HasExited)
                    {
                        LogInfo("3proxy process exited successfully.");
                    }
                    else
                    {
                        LogWarn("3proxy process did not exit after waiting.");
                    }
                }
                else
                {
                    LogInfo($"Process already exited for proxy ID: {proxy.Id}");
                }
            }
            finally
            {
                Processes.TryRemove(proxy.Id, out _);
                TryDeleteConfigFile(GetConfigPath(proxy));
                process.Dispose();
                LogInfo($"Removed proxy ID {proxy.Id} from process tracking.");
            }
        }
        else
        {
            LogInfo($"No process found to stop for proxy ID: {proxy.Id}");
            TryDeleteConfigFile(GetConfigPath(proxy));
        }

        if (Forwarders.TryRemove(proxy.Id, out var forwarder))
        {
            try { forwarder.Dispose(); } catch { }
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
            LogWarn($"Unable to delete runtime config: {ex.Message}");
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

    private static void LogInfo(string message)
    {
        _logger?.Info($"[ProxyProcessManager] {message}");
    }

    private static void LogWarn(string message)
    {
        _logger?.Warn($"[ProxyProcessManager] {message}");
    }

    private static void LogError(string message)
    {
        _logger?.Error($"[ProxyProcessManager] {message}");
    }
}
