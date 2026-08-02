using System.Text.Json;
using YLproxy.Infrastructure.Abstractions;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Infrastructure.Services;

public sealed class UserService : IUserService
{
    private readonly string _usersFilePath;
    private readonly object _gate = new();

    public UserService(string? usersFilePath = null)
    {
        _usersFilePath = usersFilePath ?? PathResolver.ResolvePath("data", "users.json");
        var dir = Path.GetDirectoryName(_usersFilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    public IReadOnlyList<User> GetAll()
    {
        lock (_gate)
        {
            return LoadUnsafe()
                .OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public User AddOrUpdate(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(user.Username))
            throw new ArgumentException("Username is required.", nameof(user));

        lock (_gate)
        {
            var users = LoadUnsafe();
            var now = DateTime.UtcNow;
            var existing = users.FirstOrDefault(u =>
                string.Equals(u.Id, user.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.Username, user.Username, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                var created = new User
                {
                    Id = string.IsNullOrWhiteSpace(user.Id) ? Guid.NewGuid().ToString("N") : user.Id,
                    Username = user.Username.Trim(),
                    DisplayName = user.DisplayName?.Trim() ?? string.Empty,
                    Role = string.IsNullOrWhiteSpace(user.Role) ? "Operator" : user.Role.Trim(),
                    IsEnabled = user.IsEnabled,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                };
                users.Add(created);
                SaveUnsafe(users);
                return created;
            }

            existing.DisplayName = user.DisplayName?.Trim() ?? existing.DisplayName;
            existing.Role = string.IsNullOrWhiteSpace(user.Role) ? existing.Role : user.Role.Trim();
            existing.IsEnabled = user.IsEnabled;
            existing.UpdatedAtUtc = now;
            SaveUnsafe(users);
            return existing;
        }
    }

    public bool Remove(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        lock (_gate)
        {
            var users = LoadUnsafe();
            var idx = users.FindIndex(u => string.Equals(u.Id, id, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                return false;

            users.RemoveAt(idx);
            SaveUnsafe(users);
            return true;
        }
    }

    public User? FindByUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        lock (_gate)
        {
            return LoadUnsafe().FirstOrDefault(u =>
                string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    private List<User> LoadUnsafe()
    {
        if (!File.Exists(_usersFilePath))
            return [];

        var json = File.ReadAllText(_usersFilePath);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<User>>(json) ?? [];
    }

    private void SaveUnsafe(List<User> users)
    {
        var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_usersFilePath, json);
    }
}
