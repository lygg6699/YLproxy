using System.Text.Json;
using YLproxy.Core;
using YLproxy.Core.Config;
using YLproxy.Utils;

namespace YLproxy.Tests;

[Trait("Category", "Unit")]
public class PathResolverTests
{
    [Fact]
    public void ResolvePath_ShouldReturnExistingRepositoryPaths()
    {
        var runtimePath = PathResolver.ResolvePath("runtime", "3proxy");
        Assert.True(Directory.Exists(runtimePath), $"Expected runtime path to exist: {runtimePath}");

        var configPath = PathResolver.ResolvePath("data", "config.json");
        Assert.Equal(
            Path.Combine(PathResolver.GetRepositoryRoot(), "data", "config.json"),
            configPath);
        Assert.DoesNotContain(
            Path.Combine("src", "YLproxy.GUI"),
            configPath,
            StringComparison.OrdinalIgnoreCase);

        if (File.Exists(configPath))
        {
            var service = new ProxyDataService("data/config.json");
            Assert.Equal(configPath, service.ConfigPath);
        }
    }

    [Fact]
    public void AppSettings_ShouldBeValidJsonAtRepositoryRoot()
    {
        var settingsPath = PathResolver.ResolvePath("AppSettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("Logging", out _));
        Assert.True(root.TryGetProperty("Proxy", out _));
        Assert.True(root.TryGetProperty("ThreeProxy", out _));
        Assert.False(root.TryGetProperty("Application", out _));
    }
}
