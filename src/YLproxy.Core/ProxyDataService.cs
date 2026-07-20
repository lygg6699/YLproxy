using System.IO;
using System.Threading;
using YLproxy.Core.Abstractions;
using YLproxy.Core.Config;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Core;

/// <summary>
/// Service for managing proxy data with atomic file operations and thread safety.
/// Wraps ProxyDataSerializer with semaphore-based locking and atomic write guarantees.
/// </summary>
public sealed class ProxyDataService : IProxyDataService
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly string _configPath;
    private readonly ProxyDataSerializer _serializer;
    private readonly bool _skipPathValidation;

    public string ConfigPath => _configPath;

    public ProxyDataService(string configPath, bool skipPathValidation = false)
    {
        _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
        _skipPathValidation = skipPathValidation;
        _serializer = new ProxyDataSerializer();

        // Validate path is canonical (not GUI-relative)
        if (!_skipPathValidation && _configPath.Contains("src/YLproxy.GUI", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Cannot use GUI-relative paths. Use repository-relative paths instead.", nameof(configPath));
        }

        // Convert to absolute path using PathResolver for repository-relative paths
        if (!Path.IsPathRooted(_configPath))
        {
            // Split path segments for PathResolver
            var segments = _configPath.Split('/', '\\');
            _configPath = PathResolver.ResolvePath(segments);
        }
    }

    /// <summary>
    /// Loads the proxy configuration from the file system.
    /// </summary>
    public AppConfig Load()
    {
        _semaphore.Wait();
        try
        {
            if (!_skipPathValidation && !File.Exists(_configPath))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(_configPath);
            var requiresMigration = false;
            var config = _serializer.Deserialize(json, out requiresMigration);

            return config;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Performs migration if needed (separate from Load to avoid unexpected writes).
    /// </summary>
    public void MigrateIfNeeded()
    {
        _semaphore.Wait();
        try
        {
            if (!_skipPathValidation && !File.Exists(_configPath))
            {
                return;
            }

            var json = File.ReadAllText(_configPath);
            var requiresMigration = false;
            var config = _serializer.Deserialize(json, out requiresMigration);

            if (requiresMigration)
            {
                // Auto-migrate: re-encrypt credentials
                Save(config);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Saves the proxy configuration to the file system atomically.
    /// </summary>
    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _semaphore.Wait();
        try
        {
            var json = _serializer.Serialize(config);
            var tempPath = _configPath + ".tmp";

            try
            {
                // Write to temp file first
                File.WriteAllText(tempPath, json);

                // Atomic replace
                if (File.Exists(_configPath))
                {
                    File.Replace(tempPath, _configPath, null);
                }
                else
                {
                    File.Move(tempPath, _configPath);
                }
            }
            catch
            {
                // Clean up temp file on failure
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
