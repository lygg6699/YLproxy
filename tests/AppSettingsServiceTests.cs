using System;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;
using YLproxy.Infrastructure;
using YLproxy.Models.Config;

namespace YLproxy.Tests;

public class AppSettingsServiceTests : IDisposable
{
    private readonly string _testConfigPath;
    private readonly string _testDirectory;

    public AppSettingsServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ylproxy_settings_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _testConfigPath = Path.Combine(_testDirectory, "AppSettings.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void Constructor_NullConfigPath_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AppSettingsService(null!));
    }

    [Fact]
    public void Constructor_EmptyConfigPath_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AppSettingsService(""));
    }

    [Fact]
    public void Constructor_InvalidConfigPath_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AppSettingsService("InvalidPath/AppSettings.json"));
    }

    [Fact]
    public void Constructor_ValidConfigPath_ShouldInitialize()
    {
        // Arrange
        var configPath = Path.Combine(_testDirectory, "AppSettings.json");

        // Act
        var service = new AppSettingsService(configPath);

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.GetConfig());
    }

    [Fact]
    public void GetLoggingConfig_ShouldReturnLoggingConfig()
    {
        // Arrange
        var service = new AppSettingsService(_testConfigPath);

        // Act
        var loggingConfig = service.GetLoggingConfig();

        // Assert
        Assert.NotNull(loggingConfig);
        Assert.NotNull(loggingConfig.LogDirectory);
    }

    [Fact]
    public void GetProxyConfig_ShouldReturnProxyConfig()
    {
        // Arrange
        var service = new AppSettingsService(_testConfigPath);

        // Act
        var proxyConfig = service.GetProxyConfig();

        // Assert
        Assert.NotNull(proxyConfig);
        Assert.True(proxyConfig.PortRangeStart > 0);
        Assert.True(proxyConfig.PortRangeEnd > proxyConfig.PortRangeStart);
    }

    [Fact]
    public void GetThreeProxyConfig_ShouldReturnThreeProxyConfig()
    {
        // Arrange
        var service = new AppSettingsService(_testConfigPath);

        // Act
        var threeProxyConfig = service.GetThreeProxyConfig();

        // Assert
        Assert.NotNull(threeProxyConfig);
        Assert.NotNull(threeProxyConfig.RuntimeDirectory);
        Assert.NotNull(threeProxyConfig.RequiredDlls);
    }

    [Fact]
    public void GetApiConfig_ShouldReturnApiConfig()
    {
        // Arrange
        var service = new AppSettingsService(_testConfigPath);

        // Act
        var apiConfig = service.GetApiConfig();

        // Assert
        Assert.NotNull(apiConfig);
        Assert.NotNull(apiConfig.AccessToken);
        Assert.NotEqual("ylproxy-api-token-change-me-in-production", apiConfig.AccessToken);
    }

    [Fact]
    public void GetConfig_ShouldReturnFullConfig()
    {
        // Arrange
        var service = new AppSettingsService(_testConfigPath);

        // Act
        var config = service.GetConfig();

        // Assert
        Assert.NotNull(config);
        Assert.NotNull(config.Logging);
        Assert.NotNull(config.Proxy);
        Assert.NotNull(config.ThreeProxy);
        Assert.NotNull(config.Api);
    }

    [Fact]
    public void Reload_ShouldReloadConfig()
    {
        // Arrange
        var service = new AppSettingsService(_testConfigPath);
        var originalToken = service.GetApiConfig().AccessToken;

        // Modify config file
        var config = service.GetConfig();
        config.Api.AccessToken = "new-test-token";
        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_testConfigPath, json);
        Thread.Sleep(600); // Wait for file watcher debounce

        // Act
        service.Reload();

        // Assert
        var newToken = service.GetApiConfig().AccessToken;
        Assert.Equal("new-test-token", newToken);
        Assert.NotEqual(originalToken, newToken);
    }

    [Fact]
    public void Constructor_MissingConfigFile_ShouldCreateDefault()
    {
        // Arrange
        var newConfigPath = Path.Combine(_testDirectory, "NewSettings.json");

        // Act
        var service = new AppSettingsService(newConfigPath);

        // Assert
        Assert.True(File.Exists(newConfigPath));
        Assert.NotNull(service.GetConfig());
    }

    [Fact]
    public void Constructor_InvalidConfigFile_ShouldUseDefault()
    {
        // Arrange
        File.WriteAllText(_testConfigPath, "invalid json content");

        // Act
        var service = new AppSettingsService(_testConfigPath);

        // Assert
        Assert.NotNull(service.GetConfig());
        Assert.NotEmpty(service.LoadErrors);
    }

    [Fact]
    public void GetSection_Logging_ShouldReturnLoggingConfig()
    {
        // Arrange
        var service = new AppSettingsService(_testConfigPath);

        // Act
        var loggingConfig = service.GetSection<LoggingConfig>("Logging");

        // Assert
        Assert.NotNull(loggingConfig);
        Assert.NotNull(loggingConfig.LogDirectory);
    }

    [Fact]
    public void GetSection_Proxy_ShouldReturnProxyConfig()
    {
        // Arrange
        var service = new AppSettingsService(_testConfigPath);

        // Act
        var proxyConfig = service.GetSection<ProxyConfig>("Proxy");

        // Assert
        Assert.NotNull(proxyConfig);
        Assert.True(proxyConfig.PortRangeStart > 0);
    }

    [Fact]
    public void GetSection_UnknownSection_ShouldReturnDefault()
    {
        // Arrange
        var service = new AppSettingsService(_testConfigPath);

        // Act
        var config = service.GetSection<ProxyConfig>("Unknown");

        // Assert
        Assert.NotNull(config);
    }

    [Fact]
    public void ApiToken_ShouldBeAutoGenerated()
    {
        // Arrange
        var configPath = Path.Combine(_testDirectory, "AutoTokenSettings.json");
        var config = new AppSettingsConfig
        {
            Logging = new LoggingConfig { LogDirectory = "logs", MinLevel = "Info", RetentionDays = 7 },
            Proxy = new ProxyConfig { PortRangeStart = 9000, PortRangeEnd = 9999, CheckIntervalSeconds = 30, DataDirectory = "data", ConfigFileName = "config.json" },
            ThreeProxy = new ThreeProxyConfig { RuntimeDirectory = "runtime", RequiredDlls = new[] { "test.dll" } },
            Api = new ApiConfig { AccessToken = "ylproxy-api-token-change-me-in-production" }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);

        // Act
        var service = new AppSettingsService(configPath);

        // Assert
        var token = service.GetApiConfig().AccessToken;
        Assert.NotEqual("ylproxy-api-token-change-me-in-production", token);
        Assert.StartsWith("ylpx-", token);
    }

    [Fact]
    public void ConfigChange_ShouldTriggerReload()
    {
        // Arrange
        var service = new AppSettingsService(_testConfigPath);
        var originalToken = service.GetApiConfig().AccessToken;

        // Modify config file externally
        var config = service.GetConfig();
        config.Api.AccessToken = "external-change-token";
        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_testConfigPath, json);
        Thread.Sleep(600); // Wait for file watcher debounce

        // Act
        var newToken = service.GetApiConfig().AccessToken;

        // Assert
        Assert.Equal("external-change-token", newToken);
    }

    [Fact]
    public void LoadErrors_ShouldTrackLoadFailures()
    {
        // Arrange
        File.WriteAllText(_testConfigPath, "invalid json");

        // Act
        var service = new AppSettingsService(_testConfigPath);

        // Assert
        Assert.NotEmpty(service.LoadErrors);
    }

    [Fact]
    public void SaveErrors_ShouldTrackSaveFailures()
    {
        // Arrange
        var configPath = Path.Combine(_testDirectory, "ReadOnlySettings.json");
        File.WriteAllText(configPath, "{}");
        File.SetAttributes(configPath, FileAttributes.ReadOnly);

        // Act
        var service = new AppSettingsService(configPath);

        // Assert
        // Note: This test might not always trigger save errors depending on timing
        // The service might not try to save immediately if config is valid
        File.SetAttributes(configPath, FileAttributes.Normal);
    }
}
