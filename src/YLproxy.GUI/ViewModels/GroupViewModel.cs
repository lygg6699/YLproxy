using System;
using System.Collections.ObjectModel;
using System.Linq;
using YLproxy.Core;
using YLproxy.Core.Abstractions;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.GUI.ViewModels;

/// <summary>
/// 负责代理分组管理，包括分组列表、分组过滤、分组CRUD操作
/// </summary>
public sealed class GroupViewModel : ViewModelBase
{
    private readonly ObservableCollection<string> _groups = new();
    private string _selectedGroup = string.Empty;
    private bool _showAllGroups = true;
    private readonly IProxyDataService? _proxyDataService;
    private readonly ILogger? _logger;

    /// <summary>
    /// 所有分组列表
    /// </summary>
    public ObservableCollection<string> Groups => _groups;

    /// <summary>
    /// 当前选中的分组
    /// </summary>
    public string SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            SetProperty(ref _selectedGroup, value);
            ShowAllGroups = string.IsNullOrEmpty(value) || value == "全部";
        }
    }

    /// <summary>
    /// 是否显示所有分组（包括"全部"选项）
    /// </summary>
    public bool ShowAllGroups
    {
        get => _showAllGroups;
        set => SetProperty(ref _showAllGroups, value);
    }

    public GroupViewModel()
    {
        _proxyDataService = null;
        _logger = null;
        LoadGroups();
    }

    public GroupViewModel(IProxyDataService proxyDataService, ILogger? logger = null)
    {
        _proxyDataService = proxyDataService;
        _logger = logger;
        LoadGroups();
    }

    /// <summary>
    /// 从数据服务加载分组列表
    /// </summary>
    public void LoadGroups()
    {
        _groups.Clear();
        _groups.Add("全部"); // 默认选项

        if (_proxyDataService == null) return;

        try
        {
            var config = _proxyDataService.Load();
            var groupNames = config.Proxies
                .Select(p => p.Group)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct()
                .OrderBy(g => g);

            foreach (var g in groupNames)
            {
                _groups.Add(g);
            }
        }
        catch (Exception ex)
        {
            _logger?.Warn($"GroupViewModel: failed to load groups: {ex.Message}");
        }
    }

    /// <summary>
    /// 从代理列表加载分组
    /// </summary>
    public void LoadGroupsFromProxies(ObservableCollection<ProxyItem> proxies)
    {
        _groups.Clear();
        _groups.Add("全部");

        var groupNames = proxies
            .Select(p => p.Group)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct()
            .OrderBy(g => g);

        foreach (var g in groupNames)
        {
            _groups.Add(g);
        }
    }

    /// <summary>
    /// 添加新分组
    /// </summary>
    public void AddGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;
        groupName = groupName.Trim();

        if (!_groups.Contains(groupName))
        {
            _groups.Add(groupName);
        }
    }

    /// <summary>
    /// 删除分组
    /// </summary>
    public void RemoveGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName) || groupName == "全部") return;
        _groups.Remove(groupName);

        if (SelectedGroup == groupName)
        {
            SelectedGroup = "全部";
        }
    }

    /// <summary>
    /// 重命名分组
    /// </summary>
    public void RenameGroup(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return;
        if (oldName == "全部" || newName == "全部") return;
        if (oldName == newName) return;

        var index = _groups.IndexOf(oldName);
        if (index >= 0)
        {
            _groups[index] = newName;
            if (SelectedGroup == oldName)
            {
                SelectedGroup = newName;
            }
        }
    }
}

