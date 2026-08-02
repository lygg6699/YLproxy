using YLproxy.Models;

namespace YLproxy.Infrastructure.Abstractions;

public interface IUserService
{
    IReadOnlyList<User> GetAll();

    User AddOrUpdate(User user);

    bool Remove(string id);

    User? FindByUsername(string username);
}
