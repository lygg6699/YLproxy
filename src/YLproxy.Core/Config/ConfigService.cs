using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YLproxy.Models;

namespace YLproxy.Core.Config;

public sealed class ConfigService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
    };

    public string ConfigPath { get; }

    public ConfigService(string configPath)
    {
        ConfigPath = configPath;
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
            // fallback to default rather than crashing GUI
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

