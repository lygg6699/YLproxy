using YLproxy.Infrastructure.Services;
using YLproxy.Models;

namespace YLproxy.Tests;

[Trait("Category", "Unit")]
public sealed class UserServiceTests
{
    [Fact]
    public void AddOrUpdate_ThenFindAndList_WorksAsExpected()
    {
        var root = CreateTempDirectory();
        try
        {
            var usersPath = Path.Combine(root, "users.json");
            var service = new UserService(usersPath);

            var created = service.AddOrUpdate(new User
            {
                Username = "admin",
                DisplayName = "Administrator",
                Role = "Admin",
                IsEnabled = true,
            });

            Assert.False(string.IsNullOrWhiteSpace(created.Id));
            Assert.Equal("admin", created.Username);

            var loaded = service.FindByUsername("ADMIN");
            Assert.NotNull(loaded);
            Assert.Equal("Administrator", loaded!.DisplayName);

            var all = service.GetAll();
            Assert.Single(all);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void Remove_ExistingUser_ReturnsTrueAndDeletes()
    {
        var root = CreateTempDirectory();
        try
        {
            var usersPath = Path.Combine(root, "users.json");
            var service = new UserService(usersPath);

            var created = service.AddOrUpdate(new User { Username = "operator", DisplayName = "Op" });
            Assert.True(service.Remove(created.Id));
            Assert.Null(service.FindByUsername("operator"));
            Assert.Empty(service.GetAll());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ylproxy-user-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Ignore cleanup failures in tests.
        }
    }
}
