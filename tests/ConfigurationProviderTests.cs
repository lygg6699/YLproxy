using YLproxy.Infrastructure;
using YLproxy.Infrastructure.Abstractions;

namespace YLproxy.Tests;

[Trait("Category", "Unit")]
public class ConfigurationProviderTests
{
    [Fact]
    public void JsonConfigurationProvider_WithInvalidPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new JsonConfigurationProvider(""));
    }

    [Fact]
    public void JsonConfigurationProvider_WithNonExistentOptionalFile_ReturnsNullSection()
    {
        using var provider = new JsonConfigurationProvider("nonexistent-config.json", optional: true, watchChanges: false);
        var section = provider.GetSection<TestConfigSection>("TestSection");
        Assert.Null(section);
    }

    [Fact]
    public void JsonConfigurationProvider_WithNonExistentOptionalFile_GetValueReturnsNull()
    {
        using var provider = new JsonConfigurationProvider("nonexistent-config.json", optional: true, watchChanges: false);
        var value = provider.GetValue("TestKey");
        Assert.Null(value);
    }

    [Fact]
    public void EnvironmentConfigurationProvider_GetValue_ReturnsEnvironmentVariable()
    {
        // Arrange
        var key = "YL_PX_TEST_KEY_" + Guid.NewGuid().ToString("N")[..8];
        var expected = "test_value_123";
        Environment.SetEnvironmentVariable(key, expected);

        try
        {
            var provider = new EnvironmentConfigurationProvider();

            // Act
            var value = provider.GetValue(key);

            // Assert
            Assert.Equal(expected, value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void EnvironmentConfigurationProvider_TryGetValue_WithExistingKey_ReturnsTrue()
    {
        var key = "YL_PX_TEST_KEY_" + Guid.NewGuid().ToString("N")[..8];
        Environment.SetEnvironmentVariable(key, "exists");

        try
        {
            var provider = new EnvironmentConfigurationProvider();
            var result = provider.TryGetValue(key, out var value);

            Assert.True(result);
            Assert.Equal("exists", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void EnvironmentConfigurationProvider_TryGetValue_WithNonExistentKey_ReturnsFalse()
    {
        var provider = new EnvironmentConfigurationProvider();
        var result = provider.TryGetValue("NONEXISTENT_VAR_" + Guid.NewGuid(), out var value);

        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void ConfigurationManager_WithSingleProvider_ReturnsValue()
    {
        var key = "YL_PX_TEST_KEY_" + Guid.NewGuid().ToString("N")[..8];
        Environment.SetEnvironmentVariable(key, "from_env");

        try
        {
            var manager = new ConfigurationManager();
            manager.AddProvider(new EnvironmentConfigurationProvider());

            var value = manager.GetValue(key);
            Assert.Equal("from_env", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void ConfigurationManager_WithMultipleProviders_FirstWins()
    {
        // Environment provider should take precedence
        var key = "YL_PX_TEST_KEY_" + Guid.NewGuid().ToString("N")[..8];
        Environment.SetEnvironmentVariable(key, "from_env");
        Environment.SetEnvironmentVariable(key + "_FALLBACK", "from_fallback");

        try
        {
            var manager = new ConfigurationManager();
            // Add environment provider first (higher priority)
            manager.AddProvider(new EnvironmentConfigurationProvider());
            // Add a mock fallback via file
            using var jsonProvider = new JsonConfigurationProvider("nonexistent-config.json", optional: true, watchChanges: false);
            manager.AddProvider(jsonProvider);

            var value = manager.GetValue(key);
            Assert.Equal("from_env", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
            Environment.SetEnvironmentVariable(key + "_FALLBACK", null);
        }
    }

    [Fact]
    public void ConfigurationManager_CacheEnabled_ReturnsCachedValue()
    {
        var key = "YL_PX_TEST_CACHE_" + Guid.NewGuid().ToString("N")[..8];
        Environment.SetEnvironmentVariable(key, "original");

        try
        {
            var manager = new ConfigurationManager();
            manager.AddProvider(new EnvironmentConfigurationProvider());
            manager.CacheEnabled = true;

            // First call should fetch and cache
            var value1 = manager.GetValue(key);
            Assert.Equal("original", value1);

            // Change environment variable after caching
            Environment.SetEnvironmentVariable(key, "modified");

            // Should still return cached value
            var value2 = manager.GetValue(key);
            Assert.Equal("original", value2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void ConfigurationManager_CacheDisabled_ReturnsLatestValue()
    {
        var key = "YL_PX_TEST_NOCACHE_" + Guid.NewGuid().ToString("N")[..8];
        Environment.SetEnvironmentVariable(key, "first");

        try
        {
            var manager = new ConfigurationManager();
            manager.AddProvider(new EnvironmentConfigurationProvider());
            manager.CacheEnabled = false;

            var value1 = manager.GetValue(key);
            Assert.Equal("first", value1);

            Environment.SetEnvironmentVariable(key, "second");

            var value2 = manager.GetValue(key);
            Assert.Equal("second", value2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void ConfigurationManager_ReloadAll_ClearsCache()
    {
        var key = "YL_PX_TEST_RELOAD_" + Guid.NewGuid().ToString("N")[..8];
        Environment.SetEnvironmentVariable(key, "before");

        try
        {
            var manager = new ConfigurationManager();
            manager.AddProvider(new EnvironmentConfigurationProvider());
            manager.CacheEnabled = true;

            var value1 = manager.GetValue(key);
            Assert.Equal("before", value1);

            Environment.SetEnvironmentVariable(key, "after");
            manager.ReloadAll();

            var value2 = manager.GetValue(key);
            Assert.Equal("after", value2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void ConfigurationManager_ClearCache_ForcesReFetch()
    {
        var key = "YL_PX_TEST_CLEAR_" + Guid.NewGuid().ToString("N")[..8];
        Environment.SetEnvironmentVariable(key, "old");

        try
        {
            var manager = new ConfigurationManager();
            manager.AddProvider(new EnvironmentConfigurationProvider());
            manager.CacheEnabled = true;

            manager.GetValue(key); // cache "old"

            Environment.SetEnvironmentVariable(key, "new");
            manager.ClearCache();

            var value = manager.GetValue(key);
            Assert.Equal("new", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void ConfigurationManager_WithNoProviders_ReturnsNull()
    {
        var manager = new ConfigurationManager();
        var value = manager.GetValue("AnyKey");
        Assert.Null(value);
    }

    [Fact]
    public void EnvironmentConfigurationProvider_Name_IsNotEmpty()
    {
        var provider = new EnvironmentConfigurationProvider();
        Assert.Equal("EnvironmentVariables", provider.Name);
    }

    [Fact]
    public void JsonConfigurationProvider_Name_ContainsFileName()
    {
        using var provider = new JsonConfigurationProvider("nonexistent-config.json", optional: true, watchChanges: false);
        Assert.Contains("nonexistent-config.json", provider.Name);
    }

    [Fact]
    public void ConfigurationManager_Providers_ReturnsRegisteredProviders()
    {
        var manager = new ConfigurationManager();
        Assert.Empty(manager.Providers);

        var provider = new EnvironmentConfigurationProvider();
        manager.AddProvider(provider);

        Assert.Single(manager.Providers);
        Assert.Same(provider, manager.Providers[0]);
    }

    [Fact]
    public void ConfigurationManager_InsertProvider_InsertsAtIndex()
    {
        var manager = new ConfigurationManager();
        var first = new EnvironmentConfigurationProvider();
        var second = new EnvironmentConfigurationProvider();

        manager.AddProvider(first);
        manager.InsertProvider(0, second);

        Assert.Equal(2, manager.Providers.Count);
        Assert.Same(second, manager.Providers[0]);
        Assert.Same(first, manager.Providers[1]);
    }
}

/// <summary>
/// Test configuration section model for JSON config deserialization testing.
/// </summary>
public class TestConfigSection
{
    public string? Name { get; set; }
    public int Value { get; set; }
}

