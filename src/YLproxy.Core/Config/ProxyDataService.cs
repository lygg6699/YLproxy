using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Core.Config;

public sealed class ProxyDataService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
    };

    public string ConfigPath { get; }

    public ProxyDataService(string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ConfigPath = Path.IsPathFullyQualified(configPath)
            ? Path.GetFullPath(configPath)
            : PathResolver.ResolvePath(configPath);

        var canonicalPath = PathResolver.ResolvePath("data", "config.json");
        if (!string.Equals(ConfigPath, canonicalPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Proxy data must be stored in the repository data/config.json file.", nameof(configPath));
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var empty = new AppConfig();
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? "");
                Save(empty);
                return empty;
            }

            var json = File.ReadAllText(ConfigPath);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
            return cfg ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? "");
        var json = JsonSerializer.Serialize(config ?? new AppConfig(), _jsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var empty = new AppConfig();
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? "");
                await SaveAsync(empty, cancellationToken).ConfigureAwait(false);
                return empty;
            }

            await using var fs = File.OpenRead(ConfigPath);
            var cfg = await JsonSerializer.DeserializeAsync<AppConfig>(fs, _jsonOptions, cancellationToken).ConfigureAwait(false);
            return cfg ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? "");
        return File.WriteAllTextAsync(ConfigPath, JsonSerializer.Serialize(config ?? new AppConfig(), _jsonOptions), cancellationToken);
    }
}
