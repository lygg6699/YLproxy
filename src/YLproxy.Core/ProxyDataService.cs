using System.IO;
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

        // Validate path is canonical (not GUI-relative)
        if (!_skipPathValidation && _configPath.Contains("src/YLproxy.GUI", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Cannot use GUI-relative paths. Use repository-relative paths instead.", nameof(configPath));
        }

        // Convert to absolute path using PathResolver for repository-relative paths
        if (!Path.IsPathRooted(_configPath))
        {
            // Split path segments for PathResolver
            var segments = _configPath.Split('/', '\\');
            _configPath = PathResolver.ResolvePath(segments);
        }
    }

    /// <summary>
    /// Loads the proxy configuration from the file system.
    /// Uses file lock to prevent concurrent access and handles file-not-found gracefully.
    /// </summary>
    public AppConfig Load()
    {
        _semaphore.Wait();
        try
        {
            // Use file lock for cross-process safety
            using var fileLock = new FileLock(_configPath, FileLockTimeoutMs);

            try
            {
                var json = File.ReadAllText(_configPath);
                var requiresMigration = false;
                var config = _serializer.Deserialize(json, out requiresMigration);
                return config;
            }
            catch (FileNotFoundException)
            {
                // File was deleted externally (e.g., log cleanup), return empty config
                return new AppConfig();
            }
            catch (DirectoryNotFoundException)
            {
                return new AppConfig();
            }
            catch (JsonException ex)
            {
                _logger.Warn($"ProxyDataService: config file corrupted, returning empty config: {ex.Message}");
                return new AppConfig();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Performs migration if needed (separate from Load to avoid unexpected writes).
    /// </summary>
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

                if (requiresMigration)
                {
                    _logger.Info("ProxyDataService: config migration required, re-encrypting credentials");
                    Save(config);
                }
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (JsonException ex)
            {
                _logger.Warn($"ProxyDataService: cannot migrate corrupted config: {ex.Message}");
                return;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Saves the proxy configuration to the file system atomically.
    /// Includes: pre-write backup, file lock, atomic replace with retry, and post-write validation.
    /// </summary>
    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _semaphore.Wait();
        try
        {
            // 1. Pre-write backup (keep last MaxBackups versions)
            BackupIfNeeded(_configPath);

            // 2. Serialize
            var json = _serializer.Serialize(config);
            var tempPath = _configPath + ".tmp";

            try
            {
                // 3. Write to temp file with file lock
                using (var fileLock = new FileLock(_configPath, FileLockTimeoutMs))
                {
                    File.WriteAllText(tempPath, json);
                }

                // 4. Atomic replace with retry
                SimpleRetry.Execute(() =>
                {
                    if (File.Exists(_configPath))
                    {
                        File.Replace(tempPath, _configPath, null);
                    }
                    else
                    {
                        File.Move(tempPath, _configPath);
                    }
                }, maxAttempts: 3, delayMs: 50, logger: _logger);

                // 5. Post-write integrity validation
                ValidateConfigIntegrity(_configPath);

                _logger.Debug($"ProxyDataService: saved config ({json.Length} bytes)");
            }
            catch
            {
                // Clean up temp file on failure
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch { /* Best-effort cleanup */ }
                }
                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Creates a backup of the config file before write, maintaining a rolling window of backups.
    /// </summary>
    private void BackupIfNeeded(string configPath)
    {
        if (!File.Exists(configPath))
            return;

        try
        {
            var backupDir = PathHelper.Combine(
                Path.GetDirectoryName(configPath) ?? "data",
                "backups");

            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupPath = PathHelper.Combine(backupDir, $"config_{timestamp}.json");

            File.Copy(configPath, backupPath, overwrite: false);

            // Rotate old backups: keep only the last MaxBackups
            var backupFiles = Directory.GetFiles(backupDir, "config_*.json")
                .OrderByDescending(f => f)
                .ToList();

            if (backupFiles.Count > MaxBackups)
            {
                foreach (var oldFile in backupFiles.Skip(MaxBackups))
                {
                    try { File.Delete(oldFile); }
                    catch { /* Best-effort cleanup */ }
                }
            }

            _logger.Debug($"ProxyDataService: backup created at {backupPath}");
        }
        catch (Exception ex)
        {
            // Backup failure should not block the save operation
            _logger.Warn($"ProxyDataService: backup failed (non-critical): {ex.Message}");
        }
    }

    /// <summary>
    /// Validates the integrity of a saved config file by attempting to deserialize it.
    /// </summary>
    private void ValidateConfigIntegrity(string configPath)
    {
        try
        {
            var json = File.ReadAllText(configPath);
            var requiresMigration = false;
            var config = _serializer.Deserialize(json, out requiresMigration);

            if (config is null)
            {
                throw new InvalidDataException("Deserialized config is null after save");
            }

            _logger.Debug($"ProxyDataService: integrity check passed ({config.Proxies.Count} proxies)");
        }
        catch (Exception ex)
        {
            _logger.Error($"ProxyDataService: integrity check FAILED after save: {ex.Message}");
            throw new InvalidOperationException($"Config file integrity check failed after save: {ex.Message}", ex);
        }
    }
}
