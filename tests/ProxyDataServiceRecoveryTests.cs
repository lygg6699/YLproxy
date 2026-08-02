using System.Text.Json;
using YLproxy.Core;
using YLproxy.Core.Concurrency;
using YLproxy.Core.Config;
using YLproxy.Models;

namespace YLproxy.Tests;

public sealed class ProxyDataServiceRecoveryTests
{
    /// <summary>
    /// Simulates corrupted JSON file: service should return empty config gracefully.
    /// </summary>
    [Fact]
    public void Load_CorruptedJson_ShouldReturnEmptyConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ylproxy_corrupt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");

        try
        {
            File.WriteAllText(configPath, "{this is not valid json!!!");

            var svc = new ProxyDataService(configPath, skipPathValidation: true);
            var config = svc.Load();

            Assert.NotNull(config);
            Assert.Empty(config.Proxies);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Simulates empty JSON file: service should return empty config gracefully.
    /// </summary>
    [Fact]
    public void Load_EmptyFile_ShouldReturnEmptyConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ylproxy_empty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");

        try
        {
            File.WriteAllText(configPath, "");

            var svc = new ProxyDataService(configPath, skipPathValidation: true);
            var config = svc.Load();

            Assert.NotNull(config);
            Assert.Empty(config.Proxies);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Tests that Save can round-trip data correctly.
    /// </summary>
    [Fact]
    public void SaveAndLoad_RoundTrip_ShouldPreserveData()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ylproxy_rt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");

        try
        {
            var svc = new ProxyDataService(configPath, skipPathValidation: true);
            var config = new AppConfig();
            config.Proxies.Add(new ProxyItem
            {
                Id = 1,
                Name = "test",
                RemoteHost = "1.2.3.4",
                RemotePort = 8080,
                LocalHost = "127.0.0.1",
                LocalPort = 9001,
                Status = ProxyStatus.Stopped,
            });

            svc.Save(config);

            Assert.True(File.Exists(configPath));
            var json = File.ReadAllText(configPath);
            Assert.Contains("1.2.3.4", json);

            // Load back
            var loaded = svc.Load();
            Assert.Single(loaded.Proxies);
            Assert.Equal(1, loaded.Proxies[0].Id);
            Assert.Equal("test", loaded.Proxies[0].Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies the serializer throws JsonException for type-mismatched input.
    /// </summary>
    [Fact]
    public void Deserialize_InvalidStructure_ShouldThrowJsonException()
    {
        var serializer = new ProxyDataSerializer();
        var json = "{\"Proxies\": [{\"Id\": \"not-an-int\"}]}";

        Assert.Throws<JsonException>(() => serializer.Deserialize(json, out _));
    }

    [Fact]
    public async Task Save_ConcurrentWrites_ShouldNotCorruptData()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ylproxy_concurrent_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");

        try
        {
            var svc = new ProxyDataService(configPath, skipPathValidation: true);
            var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(() =>
            {
                var cfg = new AppConfig();
                cfg.Proxies.Add(new ProxyItem
                {
                    Id = 1,
                    Name = $"Proxy-{i}",
                    RemoteHost = "1.2.3.4",
                    RemotePort = 8080 + i,
                    LocalHost = "127.0.0.1",
                    LocalPort = 9000 + i,
                    Status = ProxyStatus.Stopped,
                    CreateTime = DateTime.UtcNow,
                });
                svc.Save(cfg);
            })).ToArray();

            await Task.WhenAll(tasks);

            var loaded = svc.Load();
            Assert.Single(loaded.Proxies);
            Assert.StartsWith("Proxy-", loaded.Proxies[0].Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_FileLockTimeout_ShouldThrowTimeoutException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ylproxy_lock_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");
        File.WriteAllText(configPath, "{\"Proxies\":[]}");

        try
        {
            using var holdLock = new FileLock(configPath, timeoutMs: 200);
            var svc = new ProxyDataService(configPath, skipPathValidation: true);

            Assert.Throws<TimeoutException>(() => svc.Load());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}

