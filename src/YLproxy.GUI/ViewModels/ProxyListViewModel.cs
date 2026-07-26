using System.Collections.ObjectModel;
using System.Linq;
using YLproxy.Models;

namespace YLproxy.GUI.ViewModels;

/// <summary>
/// 负责代理列表的管理，包括代理集合、过滤、选择等功能
/// </summary>
public sealed class ProxyListViewModel : ViewModelBase
{
    private readonly ObservableCollection<ProxyItem> _proxies = new();
    private readonly ObservableCollection<ProxyItem> _filteredProxies = new();
    private string _searchText = string.Empty;

    public ObservableCollection<ProxyItem> Proxies => _proxies;
    public ObservableCollection<ProxyItem> FilteredProxies => _filteredProxies;

    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value, nameof(SearchText));
            ApplyProxyFilter();
        }
    }

    public ProxyListViewModel()
    {
        ApplyProxyFilter();
    }

    /// <summary>
    /// 添加代理到列表
    /// </summary>
    public void AddProxy(ProxyItem proxy)
    {
        _proxies.Add(proxy);
        ApplyProxyFilter();
    }

    /// <summary>
    /// 从列表中移除代理
    /// </summary>
    public void RemoveProxy(ProxyItem proxy)
    {
        _proxies.Remove(proxy);
        ApplyProxyFilter();
    }

    /// <summary>
    /// 清空代理列表
    /// </summary>
    public void ClearProxies()
    {
        _proxies.Clear();
        _filteredProxies.Clear();
    }

    /// <summary>
    /// 根据搜索文本过滤代理列表
    /// </summary>
    private void ApplyProxyFilter()
    {
        _filteredProxies.Clear();
        var query = string.IsNullOrWhiteSpace(_searchText)
            ? _proxies
            : _proxies.Where(p =>
                (p.Name?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.RemoteHost?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.RemotePort.ToString().Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                (p.Username?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Group?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.LocalPort.ToString().Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        foreach (var p in query)
            _filteredProxies.Add(p);
    }

    /// <summary>
    /// 刷新过滤列表（用于外部数据更新后）
    /// </summary>
    public void RefreshFilter()
    {
        var current = SearchText;
        SearchText = current;
    }
}
