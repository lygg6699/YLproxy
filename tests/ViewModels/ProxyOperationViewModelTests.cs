using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using YLproxy.Core.Abstractions;
using YLproxy.GUI.ViewModels;
using YLproxy.Infrastructure;
using YLproxy.Models;
using Xunit;
using ProxyProcessManagerInterface = YLproxy.Proxy.Abstractions.IProxyProcessManager;

namespace YLproxy.Tests.ViewModels;

public class ProxyOperationViewModelTests
{
    private readonly Mock<IProxyTester> _mockProxyTester;
    private readonly Mock<ProxyProcessManagerInterface> _mockProxyProcessManager;
    private readonly Mock<ILogger> _mockLogger;
    private readonly ProxyOperationViewModel _viewModel;

    public ProxyOperationViewModelTests()
    {
        _mockProxyTester = new Mock<IProxyTester>();
        _mockProxyProcessManager = new Mock<ProxyProcessManagerInterface>();
        _mockLogger = new Mock<ILogger>();
        _viewModel = new ProxyOperationViewModel(
            _mockProxyTester.Object,
            _mockProxyProcessManager.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void Constructor_InitializesPropertiesToFalse()
    {
        // Assert
        Assert.False(_viewModel.IsTesting);
        Assert.False(_viewModel.IsStarting);
        Assert.False(_viewModel.IsStopping);
    }

    [Fact]
    public async Task TestSelectedProxyAsync_NullProxy_DoesNothing()
    {
        // Act
        await _viewModel.TestSelectedProxyAsync(null);

        // Assert
        _mockProxyTester.Verify(t => t.TestAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(_viewModel.IsTesting);
    }

    [Fact]
    public async Task TestSelectedProxyAsync_SuccessfulTest_SetsStatusToRunning()
    {
        // Arrange
        var proxy = new ProxyItem { Id = 1, Name = "Test Proxy", RemoteHost = "1.2.3.4", RemotePort = 8080 };
        _mockProxyTester
            .Setup(t => t.TestAsync(
                proxy.RemoteHost, proxy.RemotePort, proxy.Username, proxy.Password,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 100L, (string?)null));

        // Act
        await _viewModel.TestSelectedProxyAsync(proxy);

        // Assert
        Assert.Equal(ProxyStatus.Running, proxy.Status);
        _mockProxyTester.Verify(t => t.TestAsync(
            proxy.RemoteHost, proxy.RemotePort, proxy.Username, proxy.Password,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(_viewModel.IsTesting);
    }

    [Fact]
    public async Task TestSelectedProxyAsync_FailedTest_SetsStatusToFailed()
    {
        // Arrange
        var proxy = new ProxyItem { Id = 1, Name = "Test Proxy", RemoteHost = "1.2.3.4", RemotePort = 8080 };
        _mockProxyTester
            .Setup(t => t.TestAsync(
                proxy.RemoteHost, proxy.RemotePort, proxy.Username, proxy.Password,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, 0L, (string?)"Connection timeout"));

        // Act
        await _viewModel.TestSelectedProxyAsync(proxy);

        // Assert
        Assert.Equal(ProxyStatus.Failed, proxy.Status);
        _mockProxyTester.Verify(t => t.TestAsync(
            proxy.RemoteHost, proxy.RemotePort, proxy.Username, proxy.Password,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(_viewModel.IsTesting);
    }

    [Fact]
    public async Task TestSelectedProxyAsync_Exception_SetsStatusToFailed()
    {
        // Arrange
        var proxy = new ProxyItem { Id = 1, Name = "Test Proxy", RemoteHost = "1.2.3.4", RemotePort = 8080 };
        _mockProxyTester
            .Setup(t => t.TestAsync(
                proxy.RemoteHost, proxy.RemotePort, proxy.Username, proxy.Password,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("Test exception"));

        // Act
        await _viewModel.TestSelectedProxyAsync(proxy);

        // Assert
        Assert.Equal(ProxyStatus.Failed, proxy.Status);
        _mockProxyTester.Verify(t => t.TestAsync(
            proxy.RemoteHost, proxy.RemotePort, proxy.Username, proxy.Password,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(_viewModel.IsTesting);
    }

    [Fact]
    public void StartSelectedProxy_NullProxy_DoesNothing()
    {
        // Act
        _viewModel.StartSelectedProxy(null);

        // Assert
        _mockProxyProcessManager.Verify(p => p.Start(It.IsAny<ProxyItem>()), Times.Never);
        Assert.False(_viewModel.IsStarting);
    }

    [Fact]
    public void StartSelectedProxy_ValidProxy_StartsProxy()
    {
        // Arrange
        var proxy = new ProxyItem { Id = 1, Name = "Test Proxy" };

        // Act
        _viewModel.StartSelectedProxy(proxy);

        // Assert
        Assert.Equal(ProxyStatus.Running, proxy.Status);
        _mockProxyProcessManager.Verify(p => p.Start(proxy), Times.Once);
        Assert.False(_viewModel.IsStarting);
    }

    [Fact]
    public void StartSelectedProxy_Exception_SetsStatusToFailed()
    {
        // Arrange
        var proxy = new ProxyItem { Id = 1, Name = "Test Proxy" };
        _mockProxyProcessManager
            .Setup(p => p.Start(It.IsAny<ProxyItem>()))
            .Throws(new System.Exception("Start exception"));

        // Act
        _viewModel.StartSelectedProxy(proxy);

        // Assert
        Assert.Equal(ProxyStatus.Failed, proxy.Status);
        _mockProxyProcessManager.Verify(p => p.Start(proxy), Times.Once);
        Assert.False(_viewModel.IsStarting);
    }

    [Fact]
    public void StopSelectedProxy_NullProxy_DoesNothing()
    {
        // Act
        _viewModel.StopSelectedProxy(null);

        // Assert
        _mockProxyProcessManager.Verify(p => p.Stop(It.IsAny<ProxyItem>()), Times.Never);
        Assert.False(_viewModel.IsStopping);
    }

    [Fact]
    public void StopSelectedProxy_ValidProxy_StopsProxy()
    {
        // Arrange
        var proxy = new ProxyItem { Id = 1, Name = "Test Proxy" };

        // Act
        _viewModel.StopSelectedProxy(proxy);

        // Assert
        Assert.Equal(ProxyStatus.Stopped, proxy.Status);
        _mockProxyProcessManager.Verify(p => p.Stop(proxy), Times.Once);
        Assert.False(_viewModel.IsStopping);
    }

    [Fact]
    public void StopSelectedProxy_Exception_DoesNotChangeStatus()
    {
        // Arrange
        var proxy = new ProxyItem { Id = 1, Name = "Test Proxy" };
        _mockProxyProcessManager
            .Setup(p => p.Stop(It.IsAny<ProxyItem>()))
            .Throws(new System.Exception("Stop exception"));

        // Act
        _viewModel.StopSelectedProxy(proxy);

        // Assert
        // Status should remain unchanged when stopping fails
        Assert.Equal(ProxyStatus.Stopped, proxy.Status); // Assuming it was already stopped
        _mockProxyProcessManager.Verify(p => p.Stop(proxy), Times.Once);
        Assert.False(_viewModel.IsStopping);
    }

    [Fact]
    public void BatchStart_NullOrEmptyList_DoesNothing()
    {
        // Act
        _viewModel.BatchStart(null);
        _viewModel.BatchStart(new List<ProxyItem>());

        // Assert
        _mockProxyProcessManager.Verify(p => p.Start(It.IsAny<ProxyItem>()), Times.Never);
        Assert.False(_viewModel.IsStarting);
    }

    [Fact]
    public void BatchStart_ValidList_StartsAllProxies()
    {
        // Arrange
        var proxies = new List<ProxyItem>
        {
            new ProxyItem { Id = 1, Name = "Proxy 1" },
            new ProxyItem { Id = 2, Name = "Proxy 2" }
        };

        // Act
        _viewModel.BatchStart(proxies);

        // Assert
        Assert.All(proxies, p => Assert.Equal(ProxyStatus.Running, p.Status));
        Assert.Equal(2, _mockProxyProcessManager.Invocations.Count);
        Assert.False(_viewModel.IsStarting);
    }

    [Fact]
    public void BatchStop_NullOrEmptyList_DoesNothing()
    {
        // Act
        _viewModel.BatchStop(null);
        _viewModel.BatchStop(new List<ProxyItem>());

        // Assert
        _mockProxyProcessManager.Verify(p => p.Stop(It.IsAny<ProxyItem>()), Times.Never);
        Assert.False(_viewModel.IsStopping);
    }

    [Fact]
    public void BatchStop_ValidList_StopsAllProxies()
    {
        // Arrange
        var proxies = new List<ProxyItem>
        {
            new ProxyItem { Id = 1, Name = "Proxy 1", Status = ProxyStatus.Running },
            new ProxyItem { Id = 2, Name = "Proxy 2", Status = ProxyStatus.Running }
        };

        // Act
        _viewModel.BatchStop(proxies);

        // Assert
        Assert.All(proxies, p => Assert.Equal(ProxyStatus.Stopped, p.Status));
        Assert.Equal(2, _mockProxyProcessManager.Invocations.Count);
        Assert.False(_viewModel.IsStopping);
    }
}

