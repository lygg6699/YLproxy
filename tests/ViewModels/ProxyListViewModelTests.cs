using System.Collections.ObjectModel;
using System.Linq;
using YLproxy.GUI.ViewModels;
using YLproxy.Models;
using Xunit;

namespace YLproxy.Tests.ViewModels;

public class ProxyListViewModelTests
{
    [Fact]
    public void Constructor_InitializesEmptyCollections()
    {
        // Arrange & Act
        var viewModel = new ProxyListViewModel();

        // Assert
        Assert.NotNull(viewModel.Proxies);
        Assert.NotNull(viewModel.FilteredProxies);
        Assert.Empty(viewModel.Proxies);
        Assert.Empty(viewModel.FilteredProxies);
        Assert.Equal(string.Empty, viewModel.SearchText);
    }

    [Fact]
    public void AddProxy_AddsToProxiesCollection()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        var proxy = new ProxyItem { Id = 1, Name = "Test Proxy" };

        // Act
        viewModel.AddProxy(proxy);

        // Assert
        Assert.Single(viewModel.Proxies);
        Assert.Contains(proxy, viewModel.Proxies);
    }

    [Fact]
    public void RemoveProxy_RemovesFromProxiesCollection()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        var proxy = new ProxyItem { Id = 1, Name = "Test Proxy" };
        viewModel.AddProxy(proxy);

        // Act
        viewModel.RemoveProxy(proxy);

        // Assert
        Assert.Empty(viewModel.Proxies);
        Assert.DoesNotContain(proxy, viewModel.Proxies);
    }

    [Fact]
    public void ClearProxies_ClearsBothCollections()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        var proxy1 = new ProxyItem { Id = 1, Name = "Test Proxy 1" };
        var proxy2 = new ProxyItem { Id = 2, Name = "Test Proxy 2" };
        viewModel.AddProxy(proxy1);
        viewModel.AddProxy(proxy2);

        // Act
        viewModel.ClearProxies();

        // Assert
        Assert.Empty(viewModel.Proxies);
        Assert.Empty(viewModel.FilteredProxies);
    }

    [Fact]
    public void SearchText_Empty_ReturnsAllProxiesInFiltered()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        var proxy1 = new ProxyItem { Id = 1, Name = "Alpha Proxy", RemoteHost = "1.1.1.1" };
        var proxy2 = new ProxyItem { Id = 2, Name = "Beta Proxy", RemoteHost = "2.2.2.2" };
        viewModel.AddProxy(proxy1);
        viewModel.AddProxy(proxy2);

        // Act
        viewModel.SearchText = string.Empty;

        // Assert
        Assert.Equal(2, viewModel.FilteredProxies.Count);
        Assert.Contains(proxy1, viewModel.FilteredProxies);
        Assert.Contains(proxy2, viewModel.FilteredProxies);
    }

    [Fact]
    public void SearchText_NonEmpty_FiltersProxiesByName()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        var proxy1 = new ProxyItem { Id = 1, Name = "Alpha Proxy", RemoteHost = "1.1.1.1" };
        var proxy2 = new ProxyItem { Id = 2, Name = "Beta Proxy", RemoteHost = "2.2.2.2" };
        viewModel.AddProxy(proxy1);
        viewModel.AddProxy(proxy2);

        // Act
        viewModel.SearchText = "Alpha";

        // Assert
        Assert.Single(viewModel.FilteredProxies);
        Assert.Contains(proxy1, viewModel.FilteredProxies);
        Assert.DoesNotContain(proxy2, viewModel.FilteredProxies);
    }

    [Fact]
    public void SearchText_NonEmpty_FiltersProxiesByRemoteHost()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        var proxy1 = new ProxyItem { Id = 1, Name = "Alpha Proxy", RemoteHost = "1.1.1.1" };
        var proxy2 = new ProxyItem { Id = 2, Name = "Beta Proxy", RemoteHost = "2.2.2.2" };
        viewModel.AddProxy(proxy1);
        viewModel.AddProxy(proxy2);

        // Act
        viewModel.SearchText = "2.2.2.2";

        // Assert
        Assert.Single(viewModel.FilteredProxies);
        Assert.Contains(proxy2, viewModel.FilteredProxies);
        Assert.DoesNotContain(proxy1, viewModel.FilteredProxies);
    }

    [Fact]
    public void SearchText_NonEmpty_FiltersProxiesByRemotePort()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        var proxy1 = new ProxyItem { Id = 1, Name = "Alpha Proxy", RemoteHost = "1.1.1.1", RemotePort = 8080 };
        var proxy2 = new ProxyItem { Id = 2, Name = "Beta Proxy", RemoteHost = "2.2.2.2", RemotePort = 8081 };
        viewModel.AddProxy(proxy1);
        viewModel.AddProxy(proxy2);

        // Act
        viewModel.SearchText = "8080";

        // Assert
        Assert.Single(viewModel.FilteredProxies);
        Assert.Contains(proxy1, viewModel.FilteredProxies);
        Assert.DoesNotContain(proxy2, viewModel.FilteredProxies);
    }

    [Fact]
    public void SearchText_NonEmpty_FiltersProxiesByUsername()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        var proxy1 = new ProxyItem { Id = 1, Name = "Alpha Proxy", Username = "user1" };
        var proxy2 = new ProxyItem { Id = 2, Name = "Beta Proxy", Username = "user2" };
        viewModel.AddProxy(proxy1);
        viewModel.AddProxy(proxy2);

        // Act
        viewModel.SearchText = "user1";

        // Assert
        Assert.Single(viewModel.FilteredProxies);
        Assert.Contains(proxy1, viewModel.FilteredProxies);
        Assert.DoesNotContain(proxy2, viewModel.FilteredProxies);
    }

    [Fact]
    public void SearchText_NonEmpty_FiltersProxiesByGroup()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        var proxy1 = new ProxyItem { Id = 1, Name = "Alpha Proxy", Group = "GroupA" };
        var proxy2 = new ProxyItem { Id = 2, Name = "Beta Proxy", Group = "GroupB" };
        viewModel.AddProxy(proxy1);
        viewModel.AddProxy(proxy2);

        // Act
        viewModel.SearchText = "GroupA";

        // Assert
        Assert.Single(viewModel.FilteredProxies);
        Assert.Contains(proxy1, viewModel.FilteredProxies);
        Assert.DoesNotContain(proxy2, viewModel.FilteredProxies);
    }

    [Fact]
    public void SearchText_NonEmpty_FiltersProxiesByLocalPort()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        var proxy1 = new ProxyItem { Id = 1, Name = "Alpha Proxy", LocalPort = 9001 };
        var proxy2 = new ProxyItem { Id = 2, Name = "Beta Proxy", LocalPort = 9002 };
        viewModel.AddProxy(proxy1);
        viewModel.AddProxy(proxy2);

        // Act
        viewModel.SearchText = "9001";

        // Assert
        Assert.Single(viewModel.FilteredProxies);
        Assert.Contains(proxy1, viewModel.FilteredProxies);
        Assert.DoesNotContain(proxy2, viewModel.FilteredProxies);
    }

    [Fact]
    public void RefreshFilter_ReappliesCurrentFilter()
    {
        // Arrange
        var viewModel = new ProxyListViewModel();
        var proxy1 = new ProxyItem { Id = 1, Name = "Alpha Proxy", RemoteHost = "1.1.1.1" };
        var proxy2 = new ProxyItem { Id = 2, Name = "Beta Proxy", RemoteHost = "2.2.2.2" };
        viewModel.AddProxy(proxy1);
        viewModel.AddProxy(proxy2);
        viewModel.SearchText = "Alpha"; // Should filter to just proxy1

        // Act
        viewModel.RefreshFilter();

        // Assert
        Assert.Single(viewModel.FilteredProxies);
        Assert.Contains(proxy1, viewModel.FilteredProxies);
        Assert.DoesNotContain(proxy2, viewModel.FilteredProxies);
    }
}