using YLproxy.Utils;

namespace YLproxy.Tests;

public class PathResolverTests
{
    [Fact]
    public void ResolvePath_ShouldReturnExistingRepositoryPaths()
    {
        var runtimePath = PathResolver.ResolvePath("runtime", "3proxy");
        Assert.True(Directory.Exists(runtimePath), $"Expected runtime path to exist: {runtimePath}");

        var configPath = PathResolver.ResolvePath("data", "config.json");
        Assert.True(File.Exists(configPath), $"Expected config file to exist: {configPath}");
    }
}
