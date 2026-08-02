using System.Collections.ObjectModel;
using YLproxy.Infrastructure.Abstractions;
using YLproxy.Models;

namespace YLproxy.GUI.ViewModels;

public sealed class UserManagementViewModel : ViewModelBase
{
    private readonly IUserService _userService;

    public ObservableCollection<User> Users { get; } = [];

    private User? _selectedUser;
    public User? SelectedUser
    {
        get => _selectedUser;
        set => SetProperty(ref _selectedUser, value);
    }

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _displayName = string.Empty;
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    private string _role = "Operator";
    public string Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public RelayCommand SaveUserCommand { get; }
    public RelayCommand RemoveUserCommand { get; }
    public RelayCommand ReloadUsersCommand { get; }

    public UserManagementViewModel(IUserService userService)
    {
        _userService = userService;

        SaveUserCommand = new RelayCommand(SaveUser, () => !string.IsNullOrWhiteSpace(Username));
        RemoveUserCommand = new RelayCommand(RemoveSelectedUser, () => SelectedUser is not null);
        ReloadUsersCommand = new RelayCommand(ReloadUsers);

        ReloadUsers();
    }

    private void SaveUser()
    {
        var baseUser = SelectedUser;
        var saved = _userService.AddOrUpdate(new User
        {
            Id = baseUser?.Id ?? Guid.NewGuid().ToString("N"),
            Username = Username.Trim(),
            DisplayName = DisplayName.Trim(),
            Role = string.IsNullOrWhiteSpace(Role) ? "Operator" : Role.Trim(),
            IsEnabled = IsEnabled,
        });

        ReloadUsers();
        SelectedUser = Users.FirstOrDefault(x => string.Equals(x.Id, saved.Id, StringComparison.OrdinalIgnoreCase));
    }

    private void RemoveSelectedUser()
    {
        if (SelectedUser is null)
            return;

        _userService.Remove(SelectedUser.Id);
        SelectedUser = null;
        ReloadUsers();
    }

    private void ReloadUsers()
    {
        Users.Clear();
        foreach (var user in _userService.GetAll())
            Users.Add(user);
    }
}
