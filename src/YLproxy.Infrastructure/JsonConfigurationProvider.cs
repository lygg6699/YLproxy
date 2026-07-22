using System.Text.Json;
using YLproxy.Infrastructure.Abstractions;
using YLproxy.Utils;
using MicrosoftConfig = Microsoft.Extensions.Configuration;

namespace YLproxy.Infrastructure;

/// <summary>
/// Configuration provider that reads settings from a JSON file.
/// Uses <see cref="Microsoft.Extensions.Configuration"/> under the hood for
/// standard JSON configuration parsing.
/// </summary>
public sealed class JsonConfigurationProvider : IConfigurationProvider, IDisposable
{
    private readonly string _filePath;
    private readonly MicrosoftConfig.IConfigurationRoot _configuration;
    private readonly FileSystemWatcher? _watcher;
    private readonly object _reloadLock = new();
    private bool _disposed;

    /// <summary>
    /// Fired when the configuration file changes on disk.
    /// </summary>
    public event EventHandler? ConfigurationChanged;

    /// <inheritdoc />
    public string Name => $"JsonConfig: {System.IO.Path.GetFileName(_filePath)}";

    /// <summary>
    /// Creates a new <see cref="JsonConfigurationProvider"/>.
    /// </summary>
    /// <param name="filePath">Path to the JSON configuration file.
    /// If relative, resolved via <see cref="PathResolver.ResolvePath"/>.</param>
    /// <param name="optional">Whether the file is optional (default: true).</param>
    /// <param name="watchChanges">Whether to watch the file for changes (default: true).</param>
    public JsonConfigurationProvider(string filePath, bool optional = true, bool watchChanges = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _filePath = System.IO.Path.IsPathFullyQualified(filePath)
            ? System.IO.Path.GetFullPath(filePath)
            : PathResolver.ResolvePath(filePath);

        var configDir = System.IO.Path.GetDirectoryName(_filePath);

        var builder = new MicrosoftConfig.ConfigurationBuilder()
            .SetBasePath(configDir ?? AppContext.BaseDirectory)
            .AddJsonFile(System.IO.Path.GetFileName(_filePath), optional: optional, reloadOnChange: watchChanges);

        _configuration = builder.Build();

        if (watchChanges)
        {
            try
            {
                _watcher = new FileSystemWatcher
                {
                    Path = configDir ?? AppContext.BaseDirectory,
                    Filter = System.IO.Path.GetFileName(_filePath),
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };
                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileChanged;
            }
            catch
            {
                // File watcher is non-critical; proceed without it.
            }
        }
    }

    /// <inheritdoc />
    public T? GetSection<T>(string sectionName) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        var section = _configuration.GetSection(sectionName);
        if (!section.Exists())
            return null;

        return section.Get<T>();
    }

    /// <inheritdoc />
    public string? GetValue(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _configuration[key];
    }

    /// <inheritdoc />
    public bool TryGetValue(string key, out string? value)
    {
        value = GetValue(key);
        return value is not null;
    }

    /// <inheritdoc />
    public void Reload()
    {
        lock (_reloadLock)
        {
            if (_configuration is MicrosoftConfig.IConfigurationRoot root)
            {
                root.Reload();
            }
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: wait briefly to avoid double-firing
        try
        {
            System.Threading.Thread.Sleep(100);
            Reload();
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Swallow watcher errors to avoid crashing
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _watcher?.Dispose();
            (_configuration as IDisposable)?.Dispose();
        }
    }
}

