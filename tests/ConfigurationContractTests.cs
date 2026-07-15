using System.Text.Json;
using YLproxy.Core.Config;
using YLproxy.Infrastructure;
using YLproxy.Utils;

namespace YLproxy.Tests;

public class ConfigurationContractTests
{
    [Fact]
    public void AppSettingsShouldUseTheCanonicalDirectoriesAndValues()
    {
        var settingsPath = PathResolver.ResolvePath("AppSettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var root = document.RootElement;

        var logging = root.GetProperty("Logging");
        Assert.Equal("logs", logging.GetProperty("LogDirectory").GetString());
        Assert.Equal(30, logging.GetProperty("RetentionDays").GetInt32());
        Assert.Equal("Info", logging.GetProperty("MinLevel").GetString());

        var proxy = root.GetProperty("Proxy");
        Assert.Equal("data", proxy.GetProperty("DataDirectory").GetString());
        Assert.Equal("config.json", proxy.GetProperty("ConfigFileName").GetString());
        var portStart = proxy.GetProperty("PortRangeStart").GetInt32();
        var portEnd = proxy.GetProperty("PortRangeEnd").GetInt32();
        Assert.InRange(portStart, 1, 65535);
        Assert.InRange(portEnd, 1, 65535);
        Assert.True(portStart <= portEnd);
        Assert.True(proxy.GetProperty("CheckIntervalSeconds").GetInt32() >= 1);

        var threeProxy = root.GetProperty("ThreeProxy");
        Assert.Equal("runtime/3proxy", threeProxy.GetProperty("RuntimeDirectory").GetString());
        Assert.All(threeProxy.GetProperty("RequiredDlls").EnumerateArray(), dll =>
            Assert.Equal(Path.GetFileName(dll.GetString()), dll.GetString()));
    }

    [Fact]
    public void RuntimePathsShouldResolveToCanonicalDirectories()
    {
        var root = PathResolver.GetRepositoryRoot();

        Assert.Equal(Path.Combine(root, "AppSettings.json"), PathResolver.ResolvePath("AppSettings.json"));
        Assert.Equal(Path.Combine(root, "data", "config.json"), PathResolver.ResolvePath("data", "config.json"));
        Assert.Equal(Path.Combine(root, "logs"), PathResolver.ResolvePath("logs"));
        Assert.Equal(Path.Combine(root, "runtime", "3proxy"), PathResolver.ResolvePath("runtime", "3proxy"));
        Assert.Equal(Path.Combine(root, "runtime", "3proxy", "cfg"), PathResolver.ResolvePath("runtime", "3proxy", "cfg"));
        Assert.Equal(Path.Combine(root, "runtime", "3proxy", "logs"), PathResolver.ResolvePath("runtime", "3proxy", "logs"));
    }

    [Fact]
    public void ProxyDataServiceShouldRejectNonCanonicalDataPaths()
    {
        Assert.Throws<ArgumentException>(() => new ProxyDataService("src/YLproxy.GUI/data/config.json"));
    }

    [Fact]
    public void GlobalConfigServiceShouldRejectNonCanonicalSettingsPaths()
    {
        Assert.Throws<ArgumentException>(() => new AppSettingsService("src/YLproxy.GUI/AppSettings.json"));
    }
}
