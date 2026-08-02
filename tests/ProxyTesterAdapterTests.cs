using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using YLproxy.Core;

namespace YLproxy.Tests;

public class ProxyTesterAdapterTests
{
    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Act
        var adapter = new ProxyTesterAdapter();

        // Assert
        Assert.NotNull(adapter);
    }

    [Fact]
    public async Task TestAsync_EmptyHost_ShouldReturnError()
    {
        // Arrange
        var adapter = new ProxyTesterAdapter();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await adapter.TestAsync("", 8080, null, null, cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task TestAsync_InvalidPort_ShouldReturnError()
    {
        // Arrange
        var adapter = new ProxyTesterAdapter();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await adapter.TestAsync("127.0.0.1", 0, null, null, cts.Token);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task TestAsync_Cancellation_ShouldReturnCancelled()
    {
        // Arrange
        var adapter = new ProxyTesterAdapter();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await adapter.TestAsync("127.0.0.1", 8080, null, null, cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("取消", result.Error);
    }
}
