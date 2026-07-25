using System.Text.Json;
using YLproxy.Core;
using YLproxy.Core.Config;
using YLproxy.Infrastructure;
using YLproxy.Models;

namespace YLproxy.Tests;

/// <summary>
/// Test-only security service that returns values as-is without encryption.
/// Used to avoid DPAPI dependency (Windows-only) in serialization tests.
/// </summary>
public sealed class TestSecurityService : ISecurityService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string encryptedText) => encryptedText;
    public bool IsEncrypted(string text) => false;
}

public sealed class ConfigMigrationTests
{
    /// <summary>
    /// Legacy config (no Version field) should auto-upgrade to version 1.0 in memory.
    /// </summary>
    [Fact]
    public void UpgradeConfigIfNeeded_LegacyConfig_ShouldSetVersion()
    {
        // Arrange - simulate deserialized legacy config
        var config = new AppConfig();
        Assert.Null(config.Version);

        // Act - this is what ProxyDataService.Load() does internally
        var upgraded = ProxyDataService.RunUpgradeConfigIfNeeded(config);

        // Assert
        Assert.True(upgraded);
        Assert.Equal("1.0", config.Version);
    }

    /// <summary>
    /// Version 1.0 config should upgrade to 1.1.
    /// </summary>
    [Fact]
    public void UpgradeConfigIfNeeded_Version10_ShouldUpgradeTo11()
    {
        // Arrange
        var config = new AppConfig { Version = "1.0" };

        // Act
        var upgraded = ProxyDataService.RunUpgradeConfigIfNeeded(config);

        // Assert
        Assert.True(upgraded);
        Assert.Equal("1.1", config.Version);
    }

    /// <summary>
    /// Current version config should not be modified.
    /// </summary>
    [Fact]
    public void UpgradeConfigIfNeeded_CurrentVersion_ShouldNotUpgrade()
    {
        // Arrange
        var config = new AppConfig { Version = "1.1" };

        // Act
        var upgraded = ProxyDataService.RunUpgradeConfigIfNeeded(config);

        // Assert
        Assert.False(upgraded);
        Assert.Equal("1.1", config.Version);
    }

    /// <summary>
    /// Future version config should not be downgraded.
    /// </summary>
    [Fact]
    public void UpgradeConfigIfNeeded_FutureVersion_ShouldNotUpgrade()
    {
        // Arrange
        var config = new AppConfig { Version = "2.0" };

        // Act
        var upgraded = ProxyDataService.RunUpgradeConfigIfNeeded(config);

        // Assert
        Assert.False(upgraded);
        Assert.Equal("2.0", config.Version);
    }

    /// <summary>
    /// JSON serialization of AppConfig should include the Version field.
    /// </summary>
    [Fact]
    public void Serialize_ShouldIncludeVersion()
    {
        // Arrange - use test security service to avoid DPAPI dependency
        var serializer = new ProxyDataSerializer(new TestSecurityService());
        var config = new AppConfig
        {
            Version = "1.1",
            Proxies = new List<ProxyItem>
            {
                new() { Id = 1, Name = "test", RemoteHost = "1.2.3.4", RemotePort = 8080, LocalPort = 9000 }
            }
        };

        // Act
        var json = serializer.Serialize(config);

        // Assert
        Assert.Contains("\"Version\"", json);
        Assert.Contains("1.1", json);
    }

    /// <summary>
    /// Save and Load round-trip preserves Version field.
    /// </summary>
    [Fact]
    public void SaveAndLoad_RoundTrip_ShouldPreserveVersion()
    {
        // Use a test security service that preserves values without encryption
        var tempDir = Path.Combine(Path.GetTempPath(), $"ylproxy_mig_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");

        try
        {
            var svc = new ProxyDataService(configPath, skipPathValidation: true);
            var config = new AppConfig
            {
                Version = "1.1",
                Proxies = new List<ProxyItem>
                {
                    new() { Id = 1, Name = "test", RemoteHost = "1.2.3.4", RemotePort = 8080, LocalPort = 9000 }
                }
            };

            svc.Save(config);

            var loaded = svc.Load();
            Assert.Equal("1.1", loaded.Version);
            Assert.Single(loaded.Proxies);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Serializing a config with null Version should use CurrentVersion constant.
    /// </summary>
    [Fact]
    public void Serialize_NullVersion_ShouldUseCurrentVersion()
    {
        // Arrange
        var serializer = new ProxyDataSerializer(new TestSecurityService());
        var config = new AppConfig
        {
            Version = null,
            Proxies = new List<ProxyItem>()
        };

        // Act
        var json = serializer.Serialize(config);

        // Assert
        Assert.Contains($"\"Version\": \"{AppConfig.CurrentVersion}\"", json);
    }

    /// <summary>
    /// Deserializing a config without Version field should produce null Version (triggering upgrade).
    /// </summary>
    [Fact]
    public void Deserialize_MissingVersion_ShouldProduceNullVersion()
    {
        // Arrange
        var serializer = new ProxyDataSerializer(new TestSecurityService());
        var json = """{"Proxies":[]}""";

        // Act
        var config = serializer.Deserialize(json, out var requiresMigration);

        // Assert
        Assert.Null(config.Version);
        Assert.False(requiresMigration); // No credentials to migrate
    }

    /// <summary>
    /// RunUpgradeConfigIfNeeded is idempotent - calling twice on legacy config yields correct final version.
    /// </summary>
    [Fact]
    public void UpgradeConfigIfNeeded_Idempotent_ShouldEndAtCurrentVersion()
    {
        // Arrange
        var config = new AppConfig();

        // Act - simulate running through all upgrades
        while (ProxyDataService.RunUpgradeConfigIfNeeded(config)) { }

        // Assert
        Assert.Equal(AppConfig.CurrentVersion, config.Version);
    }
}

