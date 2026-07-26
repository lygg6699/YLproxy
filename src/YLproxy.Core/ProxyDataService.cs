using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using YLproxy.Core.Abstractions;
using YLproxy.Core.Concurrency;
using YLproxy.Core.Config;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Core;

/// <summary>
/// Service for managing proxy data with atomic file operations and thread safety.
/// Wraps ProxyDataSerializer with file-level locking, atomic write guarantees,
/// automatic backup, and post-write integrity validation.
/// </summary>
public sealed class ProxyDataService : IProxyDataService
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly string _configPath;
    private readonly ProxyDataSerializer _serializer;
    private readonly bool _skipPathValidation;
    private readonly ILogger _logger;
    private const int MaxBackups = 5;
    private const int FileLockTimeoutMs = 5000;

    public string ConfigPath => _configPath;

    public ProxyDataService(string configPath, bool skipPathValidation = false, ILogger? logger = null)
    {
        _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
        _skipPathValidation = skipPathValidation;
        _serializer = new ProxyDataSerializer();
        _logger = logger ?? LoggerFactory.CreateLogger();

        if (!_skipPathValidation && _configPath.Contains("src/YLproxy.GUI", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Cannot use GUI-relative paths. Use repository-relative paths instead.", nameof(configPath));
        }

        if (!Path.IsPathRooted(_configPath))
        {
            var segments = _configPath.Split('/', '\\');
            _configPath = PathResolver.ResolvePath(segments);
        }
    }

    public AppConfig Load()
    {
        _semaphore.Wait();
        try
        {
            using var fileLock = new FileLock(_configPath, FileLockTimeoutMs);
            try
            {
                var json = File.ReadAllText(_configPath);
                var requiresMigration = false;
                var config = _serializer.Deserialize(json, out requiresMigration);
                RunUpgradeConfigIfNeeded(config);
                return config;
            }
            catch (FileNotFoundException) { return new AppConfig(); }
            catch (DirectoryNotFoundException) { return new AppConfig(); }
            catch (JsonException ex)
            {
                _logger.Warn($"ProxyDataService: config file corrupted, returning empty config: {ex.Message}");
                return new AppConfig();
            }
        }
        finally { _semaphore.Release(); }
    }

    public void MigrateIfNeeded()
    {
        _semaphore.Wait();
        try
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var requiresMigration = false;
                var config = _serializer.Deserialize(json, out requiresMigration);
                if (requiresMigration) { Save(config); }
            }
            catch (FileNotFoundException) { return; }
            catch (JsonException ex) { _logger.Warn($"ProxyDataService: cannot migrate corrupted config: {ex.Message}"); return; }
        }
        finally { _semaphore.Release(); }
    }

    public List<string> GetGroups()
    {
        var config = Load();
        return config.Proxies
            .Select(p => p.Group)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct()
            .OrderBy(g => g)
            .ToList();
    }

    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _semaphore.Wait();
        try
        {
            BackupIfNeeded(_configPath);
            var json = _serializer.Serialize(config);
            var tempPath = _configPath + ".tmp";
            try
            {
                using (var fileLock = new FileLock(_configPath, FileLockTimeoutMs))
                { File.WriteAllText(tempPath, json); }
                SimpleRetry.Execute(() =>
                {
                    if (File.Exists(_configPath)) { File.Replace(tempPath, _configPath, null); }
                    else { File.Move(tempPath, _configPath); }
                }, maxAttempts: 3, delayMs: 50, logger: _logger);
                ValidateConfigIntegrity(_configPath);
                _logger.Debug($"ProxyDataService: saved config ({json.Length} bytes)");
            }
            catch
            {
                if (File.Exists(tempPath)) { try { File.Delete(tempPath); } catch { } }
                throw;
            }
        }
        finally { _semaphore.Release(); }
    }

    public static bool RunUpgradeConfigIfNeeded(AppConfig config)
    {
        if (string.IsNullOrEmpty(config.Version))
        {
            config.Version = "1.0";
            return true;
        }
        if (config.Version == "1.0")
        {
            config.Version = "1.1";
            return true;
        }
        return false;
    }

    private void BackupIfNeeded(string configPath)
    {
        if (!File.Exists(configPath)) return;
        try
        {
            var backupDir = PathHelper.Combine(Path.GetDirectoryName(configPath) ?? "data", "backups");
            Directory.CreateDirectory(backupDir);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupPath = PathHelper.Combine(backupDir, $"config_{timestamp}.json");
            File.Copy(configPath, backupPath, overwrite: false);
            var backupFiles = Directory.GetFiles(backupDir, "config_*.json")
                .OrderByDescending(f => f).ToList();
            if (backupFiles.Count > MaxBackups)
            {
                foreach (var oldFile in backupFiles.Skip(MaxBackups))
                { try { File.Delete(oldFile); } catch { } }
            }
            _logger.Debug($"ProxyDataService: backup created at {backupPath}");
        }
        catch (Exception ex) { _logger.Warn($"ProxyDataService: backup failed (non-critical): {ex.Message}"); }
    }

    private void ValidateConfigIntegrity(string configPath)
    {
        try
        {
            var json = File.ReadAllText(configPath);
            var requiresMigration = false;
            var config = _serializer.Deserialize(json, out requiresMigration);
            if (config is null) { throw new InvalidDataException("Deserialized config is null after save"); }
            _logger.Debug($"ProxyDataService: integrity check passed ({config.Proxies.Count} proxies)");
        }
        catch (Exception ex)
        {
            _logger.Error($"ProxyDataService: integrity check FAILED after save: {ex.Message}");
            throw new InvalidOperationException($"Config file integrity check failed after save: {ex.Message}", ex);
        }
    }
}
