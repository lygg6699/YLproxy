using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Core.Config;

public sealed class ProxyDataService
{
    private readonly ProxyDataSerializer _serializer;

    public string ConfigPath { get; }

    public ProxyDataService(string configPath, ISecurityService? securityService = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ConfigPath = Path.IsPathFullyQualified(configPath)
            ? Path.GetFullPath(configPath)
            : PathResolver.ResolvePath(configPath);

        var canonicalPath = PathResolver.ResolvePath("data", "config.json");
        if (!string.Equals(ConfigPath, canonicalPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Proxy data must be stored in the repository data/config.json file.", nameof(configPath));

        _serializer = new ProxyDataSerializer(securityService);
    }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var empty = new AppConfig();
            Save(empty);
            return empty;
        }

        var json = File.ReadAllText(ConfigPath);
        var cfg = _serializer.Deserialize(json, out var requiresMigration);
        if (requiresMigration)
            Save(cfg);

        return cfg;
    }

    public void Save(AppConfig config)
    {
        WriteAtomically(_serializer.Serialize(config ?? new AppConfig()));
    }

    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigPath))
        {
            var empty = new AppConfig();
            await SaveAsync(empty, cancellationToken).ConfigureAwait(false);
            return empty;
        }

        var json = await File.ReadAllTextAsync(ConfigPath, cancellationToken).ConfigureAwait(false);
        var cfg = _serializer.Deserialize(json, out var requiresMigration);
        if (requiresMigration)
            await SaveAsync(cfg, cancellationToken).ConfigureAwait(false);

        return cfg;
    }

    public Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return SaveAsyncCore(config ?? new AppConfig(), cancellationToken);
    }

    private async Task SaveAsyncCore(AppConfig config, CancellationToken cancellationToken)
    {
        var tempPath = GetTemporaryPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? "");
            await File.WriteAllTextAsync(tempPath, _serializer.Serialize(config), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, ConfigPath, true);
        }
        finally
        {
            TryDeleteTemporaryFile(tempPath);
        }
    }

    private void WriteAtomically(string json)
    {
        var tempPath = GetTemporaryPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? "");
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            File.Move(tempPath, ConfigPath, true);
        }
        finally
        {
            TryDeleteTemporaryFile(tempPath);
        }
    }

    private string GetTemporaryPath()
    {
        return $"{ConfigPath}.{Guid.NewGuid():N}.tmp";
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
