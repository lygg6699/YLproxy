using Microsoft.Data.Sqlite;
using YLproxy.Core.Config;
using YLproxy.Core.Data;
using YLproxy.Infrastructure;
using YLproxy.Models;

namespace YLproxy.Tests;

public sealed class SqliteMigrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ISecurityService _securityService;

    public SqliteMigrationTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"YLproxy_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI tests require Windows.");

        _securityService = new DpapiSecurityService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private string GetConfigPath() => System.IO.Path.Combine(_tempDir, "config.json");
    private string GetDbPath() => System.IO.Path.Combine(_tempDir, "ylproxy.db");

    private ProxyDataService CreateJsonService(ISecurityService? securityService = null)
    {
        return new ProxyDataService(GetConfigPath(), securityService ?? _securityService, skipPathValidation: true);
    }

    private SqliteProxyRepository CreateSqliteRepo()
    {
        return new SqliteProxyRepository(GetDbPath(), _securityService);
    }

    private void WriteJsonConfig(AppConfig config)
    {
        var json = new ProxyDataSerializer(_securityService).Serialize(config);
        File.WriteAllText(GetConfigPath(), json);
    }

    [Fact]
    public void MigrateEmptyJsonToSqlite()
    {
        WriteJsonConfig(new AppConfig());

        var jsonService = CreateJsonService();
        using var sqliteRepo = CreateSqliteRepo();
        var migrationService = new DataMigrationService(jsonService, sqliteRepo, _securityService);

        var result = migrationService.MigrateIfNeeded();

        Assert.False(result);
        Assert.Equal(0, sqliteRepo.Count());
        Assert.True(File.Exists(System.IO.Path.Combine(_tempDir, ".migration_completed")));
    }

    [Fact]
    public void MigrateMultipleProxiesWithAllFields()
    {
        var config = new AppConfig
        {
            Proxies =
            {
                new ProxyItem
                {
                    Id = 1, Name = "Proxy-A", RemoteHost = "10.0.0.1", RemotePort = 1080,
                    Username = "user1", Password = "pass1",
                    LocalHost = "192.168.1.100", LocalPort = 9001,
                    Status = ProxyStatus.Stopped, CreateTime = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                },
                new ProxyItem
                {
                    Id = 2, Name = "Proxy-B", RemoteHost = "10.0.0.2", RemotePort = 1081,
                    Username = "", Password = "",
                    LocalHost = "192.168.1.101", LocalPort = 9002,
                    Status = ProxyStatus.Running, CreateTime = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
                },
                new ProxyItem
                {
                    Id = 3, Name = "Proxy-C", RemoteHost = "10.0.0.3", RemotePort = 1082,
                    Username = "user3", Password = "pass3",
                    LocalHost = "192.168.1.102", LocalPort = 9003,
                    Status = ProxyStatus.Failed, CreateTime = new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc),
                },
            },
        };

        WriteJsonConfig(config);

        var jsonService = CreateJsonService();
        using var sqliteRepo = CreateSqliteRepo();
        var migrationService = new DataMigrationService(jsonService, sqliteRepo, _securityService);

        var result = migrationService.MigrateIfNeeded();

        Assert.True(result);
        Assert.Equal(3, sqliteRepo.Count());

        var all = sqliteRepo.GetAll();
        Assert.Equal(3, all.Count);

        var proxyC = all.FirstOrDefault(p => p.Id == 3);
        Assert.NotNull(proxyC);
        Assert.Equal("Proxy-C", proxyC!.Name);
        Assert.Equal("10.0.0.3", proxyC.RemoteHost);
        Assert.Equal(1082, proxyC.RemotePort);
        Assert.Equal("user3", proxyC.Username);
        Assert.Equal("pass3", proxyC.Password);
        Assert.Equal("192.168.1.102", proxyC.LocalHost);
        Assert.Equal(9003, proxyC.LocalPort);
        Assert.Equal(ProxyStatus.Failed, proxyC.Status);
        Assert.Equal(new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc), proxyC.CreateTime);

        var proxyB = all.FirstOrDefault(p => p.Id == 2);
        Assert.NotNull(proxyB);
        Assert.Equal("", proxyB!.Username);
        Assert.Equal("", proxyB.Password);
    }

    [Fact]
    public void MigrationIsIdempotent()
    {
        var config = new AppConfig
        {
            Proxies =
            {
                new ProxyItem
                {
                    Id = 1, Name = "P1", RemoteHost = "10.0.0.1", RemotePort = 1080,
                    Username = "u", Password = "p",
                    LocalHost = "192.168.1.1", LocalPort = 9001,
                    Status = ProxyStatus.Stopped, CreateTime = DateTime.UtcNow,
                },
            },
        };

        WriteJsonConfig(config);

        var jsonService1 = CreateJsonService();
        using var sqliteRepo1 = CreateSqliteRepo();
        var migration1 = new DataMigrationService(jsonService1, sqliteRepo1, _securityService);

        var firstResult = migration1.MigrateIfNeeded();
        Assert.True(firstResult);
        Assert.Equal(1, sqliteRepo1.Count());

        sqliteRepo1.Dispose();

        var jsonService2 = CreateJsonService();
        using var sqliteRepo2 = CreateSqliteRepo();
        var migration2 = new DataMigrationService(jsonService2, sqliteRepo2, _securityService);

        var secondResult = migration2.MigrateIfNeeded();
        Assert.False(secondResult);
        Assert.Equal(1, sqliteRepo2.Count());
    }

    [Fact]
    public void MigrationCreatesBackupFile()
    {
        var config = new AppConfig
        {
            Proxies =
            {
                new ProxyItem
                {
                    Id = 1, Name = "P1", RemoteHost = "10.0.0.1", RemotePort = 1080,
                    LocalHost = "192.168.1.1", LocalPort = 9001,
                    Status = ProxyStatus.Stopped, CreateTime = DateTime.UtcNow,
                },
            },
        };

        WriteJsonConfig(config);

        var jsonService = CreateJsonService();
        using var sqliteRepo = CreateSqliteRepo();
        var migration = new DataMigrationService(jsonService, sqliteRepo, _securityService);

        migration.MigrateIfNeeded();

        var backupPath = System.IO.Path.Combine(_tempDir, "config.json.migration.bak");
        var markerPath = System.IO.Path.Combine(_tempDir, ".migration_completed");

        Assert.True(File.Exists(backupPath), "Backup file should exist at: " + backupPath);
        Assert.True(File.Exists(markerPath), "Marker file should exist at: " + markerPath);
    }

    [Fact]
    public void MigrationRollbackOnFailure()
    {
        var config = new AppConfig
        {
            Proxies =
            {
                new ProxyItem
                {
                    Id = 1, Name = "P1", RemoteHost = "10.0.0.1", RemotePort = 1080,
                    LocalHost = "192.168.1.1", LocalPort = 9001,
                    Status = ProxyStatus.Stopped, CreateTime = DateTime.UtcNow,
                },
                new ProxyItem
                {
                    Id = 2, Name = "P2", RemoteHost = "10.0.0.2", RemotePort = 1081,
                    LocalHost = "192.168.1.2", LocalPort = 9002,
                    Status = ProxyStatus.Stopped, CreateTime = DateTime.UtcNow,
                },
            },
        };

        WriteJsonConfig(config);

        var jsonService = CreateJsonService();

        Exception? caught = null;

        try
        {
            using var sqliteRepo = new ThrowOnSecondAddRepository(GetDbPath(), _securityService);
            var migration = new DataMigrationService(jsonService, sqliteRepo, _securityService);
            migration.MigrateIfNeeded();
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.IsType<InvalidOperationException>(caught);

        Assert.False(File.Exists(GetDbPath()), "Database file should have been deleted during rollback.");

        var backupPath = System.IO.Path.Combine(_tempDir, "config.json.migration.bak");
        Assert.True(File.Exists(backupPath), "Backup file should be preserved on failure.");

        var markerPath = System.IO.Path.Combine(_tempDir, ".migration_completed");
        Assert.False(File.Exists(markerPath), "Marker file should not exist after failed migration.");
    }

    private sealed class ThrowOnSecondAddRepository : SqliteProxyRepository
    {
        private int _addCount;

        public ThrowOnSecondAddRepository(string dbPath, ISecurityService securityService)
            : base(dbPath, securityService)
        {
        }

        public override int Add(ProxyItem proxy)
        {
            return ThrowOnSecond(proxy);
        }

        public override int AddWithTransaction(SqliteTransaction transaction, ProxyItem proxy)
        {
            return ThrowOnSecond(proxy);
        }

        private int ThrowOnSecond(ProxyItem proxy)
        {
            _addCount++;
            if (_addCount > 1)
                throw new InvalidOperationException("Simulated database failure on second insert.");

            return base.Add(proxy);
        }
    }
}
