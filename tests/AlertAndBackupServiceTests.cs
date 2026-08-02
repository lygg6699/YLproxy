using YLproxy.Infrastructure.Services;

namespace YLproxy.Tests;

[Trait("Category", "Integration")]
public sealed class AlertAndBackupServiceTests
{
    [Fact]
    public void AlertService_Raise_StoresAndRaisesEvent()
    {
        var service = new AlertService();
        var raised = 0;
        service.AlertRaised += _ => raised++;

        var record = service.Raise("Warn", "HighLatency", "Proxy latency > threshold", "MonitorService");

        Assert.Equal("Warn", record.Level);
        Assert.Equal(1, raised);

        var recent = service.GetRecent(5);
        Assert.Single(recent);
        Assert.Equal("HighLatency", recent[0].Title);
    }

    [Fact]
    public void BackupService_CreateAndRestore_WorksForConfigAndData()
    {
        var root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "AppSettings.json"), "{\"Logging\":{\"MinLevel\":\"Info\"}}");

            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            var dataConfig = Path.Combine(dataDir, "config.json");
            File.WriteAllText(dataConfig, "{\"Version\":\"1.1\",\"Proxies\":[]}");

            var service = new BackupService(root);
            var zip = service.CreateBackup("test");
            Assert.True(File.Exists(zip));

            File.WriteAllText(dataConfig, "{\"Version\":\"broken\"}");
            service.RestoreBackup(zip);

            var restored = File.ReadAllText(dataConfig);
            Assert.Contains("\"Version\":\"1.1\"", restored, StringComparison.Ordinal);

            var list = service.ListBackups();
            Assert.NotEmpty(list);
            Assert.Contains(zip, list);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ylproxy-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Ignore cleanup failures in tests.
        }
    }
}
