using System.Threading;
using System.Threading.Tasks;
using YLproxy.Models;

namespace YLproxy.Core.Abstractions;

public interface IProxyDataService
{
    string ConfigPath { get; }

    AppConfig Load();

    Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default);

    void Save(AppConfig config);

    Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default);

    bool MigrateToSqliteIfNeeded();

    Task<bool> MigrateToSqliteIfNeededAsync();
}

