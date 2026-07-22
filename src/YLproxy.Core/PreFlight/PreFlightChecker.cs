using YLproxy.Infrastructure;
using YLproxy.Models.Config;
using YLproxy.Utils;

namespace YLproxy.Core.PreFlight;

public class PreFlightResult
{
    public bool Passed { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public static class PreFlightChecker
{
    public static PreFlightResult Run(AppSettingsService? settingsService = null)
    {
        var result = new PreFlightResult();
        var root = PathResolver.GetRepositoryRoot();

        // 1. .NET Runtime check
        try
        {
            var framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            result.Warnings.Add($".NET Runtime: {framework}");
        }
        catch
        {
            result.Errors.Add("无法检测 .NET Runtime 版本。");
        }

        // 2. Config
        try
        {
            var svc = settingsService ?? new AppSettingsService(PathHelper.Combine(root, "AppSettings.json"));
            var cfg = svc.GetConfig();
            if (cfg is null)
            {
                result.Errors.Add("AppSettings.json 配置加载失败。请检查文件是否存在且格式正确。");
            }
            else
            {
                ValidateConfig(cfg, result);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"配置加载异常: {ex.Message}");
        }

        // 3. 3proxy runtime
        var threeProxyDir = PathHelper.Combine(root, "runtime", "3proxy", "bin64");
        var threeProxyExe = PathHelper.Combine(threeProxyDir, "3proxy.exe");
        if (!File.Exists(threeProxyExe))
        {
            result.Errors.Add($"3proxy.exe 未找到: {threeProxyExe}。请运行 scripts/prepare-runtime.ps1 准备运行时。");
        }

        string[] requiredDlls = ["FilePlugin.dll", "StringsPlugin.dll"];
        foreach (var dll in requiredDlls)
        {
            var dllPath = PathHelper.Combine(threeProxyDir, dll);
            if (!File.Exists(dllPath))
                result.Errors.Add($"{dll} 未找到: {dllPath}");
        }

        // 4. Data directory
        var dataDir = PathHelper.Combine(root, "data");
        try
        {
            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);

            var configFile = PathHelper.Combine(dataDir, "config.json");
            if (!File.Exists(configFile))
            {
                result.Warnings.Add("data/config.json 尚未创建，将在首次添加代理后自动生成。");
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"数据目录不可写 ({dataDir}): {ex.Message}");
        }

        // 5. Logs directory
        var logsDir = PathHelper.Combine(root, "logs");
        try
        {
            if (!Directory.Exists(logsDir))
                Directory.CreateDirectory(logsDir);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"日志目录不可写 ({logsDir}): {ex.Message}");
        }

        // 6. Disk space (>100MB)
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(root) ?? root);
            var freeMb = drive.AvailableFreeSpace / (1024 * 1024);
            if (freeMb < 100)
                result.Errors.Add($"磁盘空间不足: 可用 {freeMb} MB，需要至少 100 MB。");
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"无法检测磁盘空间: {ex.Message}");
        }

        // 7. 3proxy cfg directory
        var threeProxyCfg = PathHelper.Combine(root, "runtime", "3proxy", "cfg");
        try
        {
            if (!Directory.Exists(threeProxyCfg))
                Directory.CreateDirectory(threeProxyCfg);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"3proxy cfg 目录不可写 ({threeProxyCfg}): {ex.Message}");
        }

        result.Passed = result.Errors.Count == 0;
        return result;
    }

    private static void ValidateConfig(AppSettingsConfig cfg, PreFlightResult result)
    {
        if (cfg.Proxy.PortRangeStart < 1 || cfg.Proxy.PortRangeEnd > 65535 || cfg.Proxy.PortRangeStart > cfg.Proxy.PortRangeEnd)
            result.Errors.Add($"端口范围无效: {cfg.Proxy.PortRangeStart}-{cfg.Proxy.PortRangeEnd}");

        // Check for port overlap between API port and proxy port range
        if (cfg.Api.Port >= cfg.Proxy.PortRangeStart && cfg.Api.Port <= cfg.Proxy.PortRangeEnd)
            result.Errors.Add($"API 端口 {cfg.Api.Port} 与代理端口范围 {cfg.Proxy.PortRangeStart}-{cfg.Proxy.PortRangeEnd} 冲突。请修改 Api.Port 或 Proxy.PortRangeEnd 以避免冲突。");

        if (cfg.Proxy.CheckIntervalSeconds < 1)
            result.Errors.Add("监控间隔必须大于 0 秒。");

        if (cfg.ThreeProxy.RequiredDlls is null || cfg.ThreeProxy.RequiredDlls.Count == 0)
            result.Warnings.Add("未配置 3proxy 依赖 DLL 列表。");
    }
}
