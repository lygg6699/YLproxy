using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace YLproxy.GUI.ViewModels;

/// <summary>
/// 分组管理对话框的 ViewModel
/// </summary>
public sealed class ManageGroupsViewModel : ViewModelBase
{
    private readonly ObservableCollection<string> _groups = new();
    private string _selectedGroup = string.Empty;
    private string _newGroupName = string.Empty;
    private readonly Action<string>? _onDeleteGroup;
    private readonly Action<string, string>? _onRenameGroup;
    private readonly Action<string>? _onAddGroup;

    public ObservableCollection<string> Groups => _groups;

    public string SelectedGroup
    {
        get => _selectedGroup;
        set => SetProperty(ref _selectedGroup, value);
    }

    public string NewGroupName
    {
        get => _newGroupName;
        set => SetProperty(ref _newGroupName, value);
    }

    public ICommand AddGroupCommand { get; }
    public ICommand DeleteGroupCommand { get; }
    public ICommand RenameGroupCommand { get; }

    public ManageGroupsViewModel(
        ObservableCollection<string> existingGroups,
        Action<string>? onAddGroup = null,
        Action<string>? onDeleteGroup = null,
        Action<string, string>? onRenameGroup = null)
    {
        _onAddGroup = onAddGroup;
        _onDeleteGroup = onDeleteGroup;
        _onRenameGroup = onRenameGroup;

        // 复制分组列表（排除"全部"）
        foreach (var g in existingGroups.Where(g => g != "全部"))
        {
            _groups.Add(g);
        }

        AddGroupCommand = new RelayCommand<string>(ExecuteAddGroup, CanExecuteAddGroup);
        DeleteGroupCommand = new RelayCommand(ExecuteDeleteGroup, () => !string.IsNullOrEmpty(SelectedGroup) && SelectedGroup != "全部");
        RenameGroupCommand = new RelayCommand(ExecuteRenameGroup, () => !string.IsNullOrEmpty(SelectedGroup) && SelectedGroup != "全部");
    }

    private bool CanExecuteAddGroup(string? groupName)
    {
        return !string.IsNullOrWhiteSpace(groupName) && groupName.Trim() != "全部" && !_groups.Contains(groupName.Trim());
    }

    private void ExecuteAddGroup(string? groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;
        var name = groupName.Trim();
        if (_groups.Contains(name)) return;

        _groups.Add(name);
        _onAddGroup?.Invoke(name);
        NewGroupName = string.Empty;
    }

    private void ExecuteDeleteGroup()
    {
        if (string.IsNullOrEmpty(SelectedGroup) || SelectedGroup == "全部") return;
        var groupToDelete = SelectedGroup;
        _groups.Remove(groupToDelete);
        _onDeleteGroup?.Invoke(groupToDelete);
        SelectedGroup = _groups.FirstOrDefault() ?? string.Empty;
    }

    private void ExecuteRenameGroup()
    {
        if (string.IsNullOrEmpty(SelectedGroup) || SelectedGroup == "全部") return;
        
        var oldName = SelectedGroup;
        var newName = Microsoft.VisualBasic.Interaction.InputBox(
            "请输入新的分组名称:",
            "重命名分组",
            oldName);

        if (!string.IsNullOrWhiteSpace(newName) && newName.Trim() != oldName)
        {
            var trimmedName = newName.Trim();
            var index = _groups.IndexOf(oldName);
            if (index >= 0)
            {
                _groups[index] = trimmedName;
                _onRenameGroup?.Invoke(oldName, trimmedName);
                SelectedGroup = trimmedName;
            }
        }
    }
}

