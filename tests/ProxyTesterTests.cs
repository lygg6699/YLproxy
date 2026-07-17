using System;
using System.Threading;
using System.Threading.Tasks;
using YLproxy.Core;
using Xunit;

namespace YLproxy.Tests;

public sealed class ProxyTesterTests
{
    [Fact]
    public async Task HostEmpty_ShouldReturnHostEmpty()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (ok, latency, err) = await ProxyTester.TestAsync(" ", 123, null, null, cts.Token);
        Assert.False(ok);
        Assert.Equal(0, latency);
        Assert.Equal("host 为空", err);
    }

    [Fact]
    public async Task AuthIncomplete_ShouldReturnAuthIncomplete()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (ok, latency, err) = await ProxyTester.TestAsync("127.0.0.1", 123, "user", null, cts.Token);
        Assert.False(ok);
        Assert.Equal(0, latency);
        Assert.Equal("代理认证信息不完整", err);
    }

    [Fact]
    public async Task CancellationRequested_ShouldReturnCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var (ok, latency, err) = await ProxyTester.TestAsync("127.0.0.1", 123, null, null, maxRetries: 0, retryDelayMs: 1, cts.Token);
        Assert.False(ok);
        Assert.Equal(0, latency);
        Assert.Equal("测试已取消", err);
    }

    [Fact]
    public async Task MaxRetriesExceeded_ShouldReturnLastError()
    {
        // We force a HttpRequestException branch by using invalid proxy host.
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        ProxyTester.TimeoutMs = 200; // keep test fast

        var (ok, latency, err) = await ProxyTester.TestAsync(
            host: "256.256.256.256", // invalid IP -> will cause HttpRequestException
            port: 1,
            username: null,
            password: null,
            maxRetries: 1,
            retryDelayMs: 1,
            cancellationToken: cts.Token);

        Assert.False(ok);
        Assert.Equal(0, latency);
        Assert.NotNull(err);
        Assert.StartsWith("连接失败:", err);
    }
}

