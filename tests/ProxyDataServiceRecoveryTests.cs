using System.Text.Json;
using YLproxy.Core;
using YLproxy.Core.Config;
using YLproxy.Models;

namespace YLproxy.Tests;

public sealed class ProxyDataServiceRecoveryTests
{
    /// <summary>
    /// Simulates corrupted JSON file: deserialization must throw JsonException.
    /// </summary>
    [Fact]
    public void Load_CorruptedJson_ShouldThrowJsonException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ylproxy_corrupt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");

        try
        {
            File.WriteAllText(configPath, "{this is not valid json!!!");

            var svc = new ProxyDataService(configPath, skipPathValidation: true);
            Assert.Throws<JsonException>(() => svc.Load());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Simulates empty JSON file: deserialization must throw JsonException.
    /// </summary>
    [Fact]
    public void Load_EmptyFile_ShouldThrowJsonException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ylproxy_empty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");

        try
        {
            File.WriteAllText(configPath, "");

            var svc = new ProxyDataService(configPath, skipPathValidation: true);
            Assert.Throws<JsonException>(() => svc.Load());
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
                Id = 1, Name = "test", RemoteHost = "1.2.3.4", RemotePort = 8080,
                LocalHost = "127.0.0.1", LocalPort = 9001, Status = ProxyStatus.Stopped,
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
}

