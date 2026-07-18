using System;
using System.Net.Http;
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

    [Fact]
    public async Task Timeout_ShouldReturnTimeoutError()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ProxyTester.TestUrl = "https://example.com";
        ProxyTester.TimeoutMs = 1; // immediate timeout

        // Use a custom handler that blocks, to trigger timeout
        ProxyTester.HttpMessageHandlerFactory = () => new DelayedHandler(delayMs: 5000);

        try
        {
            var (ok, latency, err) = await ProxyTester.TestAsync(
                host: "127.0.0.1",
                port: 1234,
                username: null,
                password: null,
                maxRetries: 0,
                retryDelayMs: 0,
                cancellationToken: cts.Token);

            Assert.False(ok);
            Assert.Equal(0, latency);
            Assert.Equal("连接失败: 超时", err);
        }
        finally
        {
            ProxyTester.HttpMessageHandlerFactory = null;
            ProxyTester.TestUrl = "https://www.baidu.com";
            ProxyTester.TimeoutMs = 15000;
        }
    }

    [Fact]
    public async Task HttpRequestException_ShouldReturnConnectionFailed()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ProxyTester.HttpMessageHandlerFactory = () => new ThrowingHandler(() => throw new HttpRequestException("test hre"));

        try
        {
            var (ok, latency, err) = await ProxyTester.TestAsync(
                host: "127.0.0.1",
                port: 1234,
                username: null,
                password: null,
                maxRetries: 0,
                retryDelayMs: 0,
                cancellationToken: cts.Token);

            Assert.False(ok);
            Assert.Equal(0, latency);
            Assert.StartsWith("连接失败:", err);
        }
        finally
        {
            ProxyTester.HttpMessageHandlerFactory = null;
        }
    }

    [Fact]
    public async Task GenericException_ShouldReturnConnectionFailed()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ProxyTester.HttpMessageHandlerFactory = () => new ThrowingHandler(() => throw new InvalidOperationException("test"));

        try
        {
            var (ok, latency, err) = await ProxyTester.TestAsync(
                host: "127.0.0.1",
                port: 1234,
                username: null,
                password: null,
                maxRetries: 0,
                retryDelayMs: 0,
                cancellationToken: cts.Token);

            Assert.False(ok);
            Assert.Equal(0, latency);
            Assert.Equal("连接失败", err);
        }
        finally
        {
            ProxyTester.HttpMessageHandlerFactory = null;
        }
    }

    [Fact]
    public async Task Success_ShouldReturnSuccess()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ProxyTester.HttpMessageHandlerFactory = () => new FakeHttpMessageHandler(
            new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        try
        {
            var (ok, latency, err) = await ProxyTester.TestAsync(
                host: "127.0.0.1",
                port: 1234,
                username: null,
                password: null,
                maxRetries: 0,
                retryDelayMs: 0,
                cancellationToken: cts.Token);

            Assert.True(ok);
            Assert.True(latency >= 0);
            Assert.Null(err);
        }
        finally
        {
            ProxyTester.HttpMessageHandlerFactory = null;
        }
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        private readonly int _delayMs;
        public DelayedHandler(int delayMs) => _delayMs = delayMs;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(_delayMs, cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Func<Exception> _factory;
        public ThrowingHandler(Func<Exception> factory) => _factory = factory;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw _factory();
        }
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public FakeHttpMessageHandler(HttpResponseMessage response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}

