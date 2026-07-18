using System.IO;
using System.Text;
using YLproxy.Core.Data;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Core.Config;

public sealed class ProxyDataService
{
    private const int MaxBackupCount = 3;

    private static readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly ProxyDataSerializer _serializer;
    private readonly SqliteProxyRepository? _sqliteRepository;
    private DataMigrationService? _migrationService;
    private static readonly ILogger _logger = LoggerFactory.CreateLogger();

    public string ConfigPath { get; }

    /// <summary>
    /// Returns true if the SQLite migration has been completed and the database is the primary data source.
    /// </summary>
    public bool IsSqliteMigrated =>
        _sqliteRepository is not null &&
        File.Exists(PathResolver.ResolvePath("data", ".migration_completed"));

    /// <summary>
    /// Indicates whether credentials were reset during the most recent load operation.
    /// </summary>
    public bool CredentialsResetDuringLoad { get; private set; }

    public ProxyDataService(
        string configPath,
        ISecurityService? securityService = null,
        SqliteProxyRepository? sqliteRepository = null,
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
        _sqliteRepository = sqliteRepository;

        if (_sqliteRepository is not null)
        {
            ISecurityService secSvc;
            if (securityService is not null)
            {
                secSvc = securityService;
            }
            else
            {
                if (!OperatingSystem.IsWindows())
                    throw new PlatformNotSupportedException("DPAPI credential storage requires Windows.");
                secSvc = new DpapiSecurityService();
            }
            _migrationService = new DataMigrationService(this, _sqliteRepository, secSvc);
        }
    }

    /// <summary>
    /// Triggers the JSON-to-SQLite migration if it hasn't been performed yet.
    /// </summary>
    public async Task<bool> MigrateToSqliteIfNeededAsync()
    {
        if (_migrationService is null)
            return false;

        return await System.Threading.Tasks.Task.Run(() => _migrationService.MigrateIfNeeded())
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronous version for callers that do not need async.
    /// </summary>
    public bool MigrateToSqliteIfNeeded()
    {
        return _migrationService?.MigrateIfNeeded() ?? false;
    }

    public AppConfig Load()
    {
        CredentialsResetDuringLoad = false;

        // If SQLite is available and migration has occurred, load from SQLite
        if (IsSqliteMigrated)
        {
            return LoadFromSqlite();
        }

        // Otherwise, fall back to JSON
        return LoadFromJson();
    }

    private AppConfig LoadFromSqlite()
    {
        var config = new AppConfig();
        var proxies = _sqliteRepository!.GetAll();
        config.Proxies.AddRange(proxies);
        return config;
    }

    private AppConfig LoadFromJson()
    {
        _ioLock.WaitAsync().GetAwaiter().GetResult();
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
        _ioLock.WaitAsync().GetAwaiter().GetResult();
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

        // Always write JSON (primary persistence in transition period)
        WriteAtomically(json);

        // Dual-write to SQLite if available
        if (_sqliteRepository is not null)
        {
            SyncToSqlite(config);
        }
    }

    private void SyncToSqlite(AppConfig config)
    {
        try
        {
            // Full sync: delete all and re-insert from the canonical JSON state.
            // This is the safest strategy during the transition period.
            var existing = _sqliteRepository!.GetAll();
            var configIds = new HashSet<int>(config.Proxies.Select(p => p.Id));

            // Remove proxies that no longer exist in config
            foreach (var existingProxy in existing)
            {
                if (!configIds.Contains(existingProxy.Id))
                {
                    _sqliteRepository.Delete(existingProxy.Id);
                }
            }

            // Upsert proxies from config
            foreach (var proxy in config.Proxies)
            {
                var existingProxy = _sqliteRepository.GetById(proxy.Id);
                if (existingProxy is not null)
                {
                    _sqliteRepository.Update(proxy);
                }
                else
                {
                    _sqliteRepository.Add(proxy);
                }
            }
        }
        catch (Exception ex)
        {
            // Dual-write failure should not block the JSON save.
            _logger.Warn($"SQLite dual-write sync failed: {ex.Message}");
        }
    }

    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        CredentialsResetDuringLoad = false;

        if (IsSqliteMigrated)
        {
            return await System.Threading.Tasks.Task.Run(LoadFromSqlite, cancellationToken)
                .ConfigureAwait(false);
        }

        // Use async lock variant
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

    private void WriteAtomically(string json)
    {
        var tempPath = GetTemporaryPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? "");
            // Write temp file with exclusive write, allowing concurrent readers
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

    /// <summary>
    /// Writes the current JSON state to a rotating backup slot (config.json.bak.0..N).
    /// </summary>
    private void RotateBackups(string json)
    {
        // Rotate existing backups: config.json.bak.2 → config.json.bak.1, etc.
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

        // Write the freshest backup as .bak.0
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

    /// <summary>
    /// Attempts to recover from a corrupted config.json by trying backups in order,
    /// then falls back to an empty config.
    /// </summary>
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
                    // Restore from this backup
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
