using System;
using YLproxy.Models;
using YLproxy.Models.Config;
using YLproxy.Proxy;
using Xunit;

namespace YLproxy.Tests.Proxy;

public sealed class ProxyProcessManagerAdapterTests
{
    [Fact]
    public void Ctor_NullManager_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new ProxyProcessManagerAdapter(null!));
    }

    [Fact]
    public void Configure_NullSettings_ShouldThrow()
    {
        var adapter = new ProxyProcessManagerAdapter(new ProxyProcessManager());
        Assert.Throws<ArgumentNullException>(() => adapter.Configure(null!));
    }

    [Fact]
    public void IsRunning_BeforeStart_ShouldReturnFalse()
    {
        var adapter = new ProxyProcessManagerAdapter(new ProxyProcessManager());
        var proxy = new ProxyItem { Id = 1001, Name = "p", LocalHost = "127.0.0.1", LocalPort = 9001 };

        Assert.False(adapter.IsRunning(proxy));
    }

    [Fact]
    public void Stop_ProcessNotStarted_ShouldNotThrow()
    {
        var adapter = new ProxyProcessManagerAdapter(new ProxyProcessManager());
        var proxy = new ProxyItem { Id = 1002, Name = "p", LocalHost = "127.0.0.1", LocalPort = 9002 };

        adapter.Stop(proxy);

        Assert.True(true);
    }

    [Fact]
    public void Configure_ValidSettings_ShouldNotThrow()
    {
        var adapter = new ProxyProcessManagerAdapter(new ProxyProcessManager());
        var settings = new ThreeProxyConfig
        {
            RuntimeDirectory = "runtime/3proxy",
            RequiredDlls = new() { "libcrypto-3-x64.dll" }
        };

        adapter.Configure(settings);

        Assert.True(true);
    }
}
