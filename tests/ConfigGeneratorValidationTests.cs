using YLproxy.Models;
using YLproxy.Proxy;

namespace YLproxy.Tests;

public sealed class ConfigGeneratorValidationTests
{
    [Fact]
    public void Generate_EmptyRemoteHost_ShouldThrow()
    {
        var proxy = new ProxyItem
        {
            Id = 1, Name = "test", RemoteHost = "", RemotePort = 8080,
            LocalHost = "127.0.0.1", LocalPort = 9001,
        };

        var ex = Assert.Throws<ArgumentException>(() => ConfigGenerator.Generate(proxy));
        Assert.Contains("RemoteHost", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_InvalidPort_ShouldThrow()
    {
        var proxy = new ProxyItem
        {
            Id = 2, Name = "test", RemoteHost = "1.2.3.4", RemotePort = 0,
            LocalHost = "127.0.0.1", LocalPort = 9001,
        };

        var ex = Assert.Throws<ArgumentException>(() => ConfigGenerator.Generate(proxy));
        Assert.Contains("RemotePort", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_InvalidLocalPort_ShouldThrow()
    {
        var proxy = new ProxyItem
        {
            Id = 3, Name = "test", RemoteHost = "1.2.3.4", RemotePort = 8080,
            LocalHost = "127.0.0.1", LocalPort = 0,
        };

        var ex = Assert.Throws<ArgumentException>(() => ConfigGenerator.Generate(proxy));
        Assert.Contains("LocalPort", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_UsernameWithoutPassword_ShouldThrow()
    {
        var proxy = new ProxyItem
        {
            Id = 4, Name = "test", RemoteHost = "1.2.3.4", RemotePort = 8080,
            Username = "user", Password = "",
            LocalHost = "127.0.0.1", LocalPort = 9001,
        };

        var ex = Assert.Throws<ArgumentException>(() => ConfigGenerator.Generate(proxy));
        Assert.Contains("username and password", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_PasswordWithoutUsername_ShouldThrow()
    {
        var proxy = new ProxyItem
        {
            Id = 5, Name = "test", RemoteHost = "1.2.3.4", RemotePort = 8080,
            Username = "", Password = "pass",
            LocalHost = "127.0.0.1", LocalPort = 9001,
        };

        var ex = Assert.Throws<ArgumentException>(() => ConfigGenerator.Generate(proxy));
        Assert.Contains("username and password", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_ValidProxy_ShouldSucceed()
    {
        var proxy = new ProxyItem
        {
            Id = 6, Name = "valid", RemoteHost = "example.com", RemotePort = 3128,
            Username = "user", Password = "pass",
            LocalHost = "127.0.0.1", LocalPort = 9002,
        };

        var cfg = ConfigGenerator.Generate(proxy);

        Assert.NotNull(cfg);
        Assert.Contains("parent 1000 http example.com 3128 user pass", cfg);
        Assert.Contains("proxy -a -p9002", cfg);
        Assert.Contains("fakeresolve", cfg);
    }
}

