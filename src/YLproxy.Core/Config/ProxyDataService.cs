using System.IO;
using System.Text;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Core.Config;

/// <summary>
/// JSON-based proxy data persistence service.
/// Single data source: data/config.json with atomic writes and rotating backups.
/// </summary>
public sealed class ProxyDataService : Abstractions.IProxyDataService
{
    private const int MaxBackupCount = 3;

    private static readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly ProxyDataSerializer _serializer;
    private static readonly ILogger _logger = LoggerFactory.CreateLogger();

    public string ConfigPath { get; }

    /// <summary>
    /// Indicates whether credentials were reset during the most recent load operation.
    /// </summary>
    public bool CredentialsResetDuringLoad { get; private set; }

    public ProxyDataService(
        string configPath,
        ISecurityService? securityService = null,
        bool skipPathValidation = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ConfigPath = Path.IsPathFullyQualified(configPath)
            ? Path.GetFullPath(configPath)
            : PathResolver.ResolvePath(configPath);

        if (!skipPathValidation)
        {
            var canonicalPath = PathResolver.ResolvePath("data", "config.json");
            if (!string.Equals(ConfigPath, canonicalPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Proxy data must be stored in the repository data/config.json file.", nameof(configPath));
        }

        _serializer = new ProxyDataSerializer(securityService);
    }

    public AppConfig Load()
    {
        CredentialsResetDuringLoad = false;
        return LoadFromJson();
    }

    private AppConfig LoadFromJson()
    {
        _ioLock.Wait();
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var empty = new AppConfig();
                SaveInternal(empty);
                return empty;
            }

            return ReadAndDeserializeConfig();
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private AppConfig ReadAndDeserializeConfig()
    {
        try
        {
            var json = SimpleRetry.Execute(() => File.ReadAllText(ConfigPath), maxAttempts: 3, delayMs: 100, logger: _logger);
            var cfg = _serializer.Deserialize(json, out var requiresMigration);
            if (requiresMigration)
                SaveInternal(cfg);

            return cfg;
        }
        catch (AggregateException ex)
        {
            _logger.Error($"Failed to read config.json after retries: {ex.Message}", ex);
            return RecoverFromCorruption();
        }
        catch (Exception ex) when (ex is not AggregateException)
        {
            _logger.Error($"Failed to deserialize config.json: {ex.Message}", ex);
            return RecoverFromCorruption();
        }
    }

    public void Save(AppConfig config)
    {
        _ioLock.Wait();
        try
        {
            SaveInternal(config);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private void SaveInternal(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var json = _serializer.Serialize(config);

        // Create a rotating backup before overwriting
        RotateBackups(json);

        // Atomic write to JSON (single source of truth)
        WriteAtomically(json);
    }

    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        CredentialsResetDuringLoad = false;

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var empty = new AppConfig();
                SaveInternal(empty);
                return empty;
            }

            return await ReadAndDeserializeConfigAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<AppConfig> ReadAndDeserializeConfigAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = await SimpleRetry.ExecuteAsync(
                () => File.ReadAllTextAsync(ConfigPath, cancellationToken),
                maxAttempts: 3, delayMs: 100, logger: _logger).ConfigureAwait(false);
            var cfg = _serializer.Deserialize(json, out var requiresMigration);
            if (requiresMigration)
                SaveInternal(cfg);

            return cfg;
        }
        catch (AggregateException ex)
        {
            _logger.Error($"Failed to read config.json after retries: {ex.Message}", ex);
            return RecoverFromCorruption();
        }
        catch (Exception ex) when (ex is not AggregateException)
        {
            _logger.Error($"Failed to deserialize config.json: {ex.Message}", ex);
            return RecoverFromCorruption();
        }
    }

    public Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return System.Threading.Tasks.Task.Run(() => Save(config ?? new AppConfig()), cancellationToken);
    }

    public bool MigrateToSqliteIfNeeded()
    {
        // This service is JSON-based, so SQLite migration is not applicable
        // Return false to indicate no migration was performed
        return false;
    }

    public Task<bool> MigrateToSqliteIfNeededAsync()
    {
        // This service is JSON-based, so SQLite migration is not applicable
        // Return false to indicate no migration was performed
        return Task.FromResult(false);
    }

    public bool IsSqliteMigrated => false;

    private void WriteAtomically(string json)
    {
        var tempPath = GetTemporaryPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? "");
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(fs, Encoding.UTF8))
            {
                writer.Write(json);
            }
            File.Move(tempPath, ConfigPath, true);
        }
        finally
        {
            TryDeleteTemporaryFile(tempPath);
        }
    }

    private string GetTemporaryPath()
    {
        return $"{ConfigPath}.{Guid.NewGuid():N}.tmp";
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to delete temporary file '{path}': {ex.Message}");
        }
    }

    private void RotateBackups(string json)
    {
        for (int i = MaxBackupCount - 2; i >= 0; i--)
        {
            var oldPath = $"{ConfigPath}.bak.{i}";
            var newPath = $"{ConfigPath}.bak.{i + 1}";
            try
            {
                if (File.Exists(oldPath))
                    File.Move(oldPath, newPath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Backup rotation failed moving '{oldPath}' → '{newPath}': {ex.Message}");
            }
        }

        var bakPath = $"{ConfigPath}.bak.0";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(bakPath) ?? "");
            File.WriteAllText(bakPath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to write backup '{bakPath}': {ex.Message}");
        }
    }

    private AppConfig RecoverFromCorruption()
    {
        for (int i = 0; i < MaxBackupCount; i++)
        {
            var bakPath = $"{ConfigPath}.bak.{i}";
            if (!File.Exists(bakPath))
                continue;

            try
            {
                _logger.Info($"Attempting recovery from backup '{bakPath}'");
                var json = File.ReadAllText(bakPath, Encoding.UTF8);
                var cfg = _serializer.Deserialize(json, out _);
                if (cfg.Proxies.Count > 0)
                {
                    WriteAtomically(json);
                    _logger.Info($"Successfully recovered {cfg.Proxies.Count} proxies from backup '{bakPath}'");
                    return cfg;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Backup recovery failed for '{bakPath}': {ex.Message}");
            }
        }

        _logger.Error("All backup recovery attempts failed, starting with empty config");
        return new AppConfig();
    }
}
