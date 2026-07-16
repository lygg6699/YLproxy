using YLproxy.Core.Config;
using YLproxy.Infrastructure;
using YLproxy.Models;

namespace YLproxy.Core.Data;

public sealed class DataMigrationService
{
    private readonly ProxyDataService _proxyDataService;
    private readonly SqliteProxyRepository _sqliteRepository;
    private readonly ISecurityService _securityService;
    private readonly ILogger _logger;
    private readonly string _dataDir;

    public DataMigrationService(
        ProxyDataService proxyDataService,
        SqliteProxyRepository sqliteRepository,
        ISecurityService securityService,
        ILogger? logger = null)
    {
        _proxyDataService = proxyDataService ?? throw new ArgumentNullException(nameof(proxyDataService));
        _sqliteRepository = sqliteRepository ?? throw new ArgumentNullException(nameof(sqliteRepository));
        _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        _logger = logger ?? LoggerFactory.CreateLogger();

        _dataDir = System.IO.Path.GetDirectoryName(_proxyDataService.ConfigPath)
            ?? throw new InvalidOperationException("Unable to determine data directory from config path.");
    }

    private string MigrationMarkerPath => System.IO.Path.Combine(_dataDir, ".migration_completed");
    private string BackupPath => System.IO.Path.Combine(_dataDir, "config.json.migration.bak");

    /// <summary>
    /// Migrates data from JSON config to SQLite if not already migrated.
    /// Returns true if migration was performed, false if already complete.
    /// </summary>
    public bool MigrateIfNeeded()
    {
        // a) Check marker file
        if (File.Exists(MigrationMarkerPath))
        {
            _logger.Info("Migration already completed (marker file exists). Skipping.");
            return false;
        }

        // b) Check if database already has data
        if (_sqliteRepository.Count() > 0)
        {
            _logger.Info($"Database already contains {_sqliteRepository.Count()} records. Creating marker file.");
            CreateMarkerFile();
            return false;
        }

        // c) Load JSON config
        AppConfig config;
        try
        {
            config = _proxyDataService.Load();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to load JSON config for migration.", ex);
        }

        // d) Empty config
        if (config.Proxies.Count == 0)
        {
            _logger.Info("No proxy data to migrate. Creating marker file.");
            CreateMarkerFile();
            return false;
        }

        var jsonCount = config.Proxies.Count;
        _logger.Info($"Starting migration of {jsonCount} proxy records from JSON to SQLite.");

        // e) Backup
        try
        {
            File.Copy(_proxyDataService.ConfigPath, BackupPath, overwrite: true);
            _logger.Info($"Backup created: {BackupPath}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to create backup of config.json. Migration aborted.", ex);
        }

        // f) Execute migration within a transaction
        var migrated = 0;
        using var transaction = _sqliteRepository.BeginTransaction();
        try
        {
            foreach (var proxy in config.Proxies)
            {
                _sqliteRepository.AddWithTransaction(transaction, proxy);
                migrated++;
            }

            transaction.Commit();
            _logger.Info($"Inserted {migrated} proxy records into SQLite.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Migration failed after inserting {migrated} of {jsonCount} records.", ex);
            try { transaction.Rollback(); } catch (Exception rbEx) { _logger.Warn($"Transaction rollback failed: {rbEx.Message}"); }
            Rollback();
            throw new InvalidOperationException(
                $"Migration failed after inserting {migrated} of {jsonCount} records: {ex.Message}", ex);
        }

        // g) Verify record count
        var dbCount = _sqliteRepository.Count();
        if (dbCount != jsonCount)
        {
            var msg = $"Record count mismatch after migration. JSON: {jsonCount}, SQLite: {dbCount}.";
            _logger.Error(msg);
            Rollback();
            throw new InvalidOperationException(msg);
        }

        // h) Create marker file
        CreateMarkerFile();

        // i) Log completion
        _logger.Info($"Migration completed successfully: {jsonCount} records migrated.");
        return true;
    }

    private void CreateMarkerFile()
    {
        if (!Directory.Exists(_dataDir))
            Directory.CreateDirectory(_dataDir);

        File.WriteAllText(MigrationMarkerPath, DateTime.UtcNow.ToString("o"));
    }

    private void Rollback()
    {
        try
        {
            if (File.Exists(MigrationMarkerPath))
                File.Delete(MigrationMarkerPath);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to remove migration marker during rollback: {ex.Message}");
        }

        try
        {
            // Dispose the repo to close the SQLite connection before deleting files.
            _sqliteRepository.Dispose();

            // Clear the connection pool to release any lingering file handles on Windows.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var dbPaths = new[]
            {
                System.IO.Path.Combine(_dataDir, "ylproxy.db"),
                System.IO.Path.Combine(_dataDir, "ylproxy.db-shm"),
                System.IO.Path.Combine(_dataDir, "ylproxy.db-wal"),
            };

            foreach (var dbPath in dbPaths)
            {
                try
                {
                    if (File.Exists(dbPath))
                    {
                        SimpleRetry.Execute(
                            () => File.Delete(dbPath),
                            maxAttempts: 3,
                            delayMs: 50,
                            logger: _logger);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Failed to delete '{dbPath}' during rollback: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Rollback cleanup encountered an error: {ex.Message}");
        }
    }
}
