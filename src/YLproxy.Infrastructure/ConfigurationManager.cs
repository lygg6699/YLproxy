using System.Collections.Concurrent;
using YLproxy.Infrastructure.Abstractions;

namespace YLproxy.Infrastructure;

/// <summary>
/// Manages multiple configuration providers with caching and fallback semantics.
/// Providers are queried in registration order; the first non-null result wins.
/// </summary>
public sealed class ConfigurationManager : IDisposable
{
    private readonly List<IConfigurationProvider> _providers = new();
    private readonly ConcurrentDictionary<string, object?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReaderWriterLockSlim _rwLock = new();
    private bool _disposed;

    /// <summary>
    /// Gets the list of registered providers.
    /// </summary>
    public IReadOnlyList<IConfigurationProvider> Providers => _providers.AsReadOnly();

    /// <summary>
    /// Gets or sets whether caching is enabled.
    /// </summary>
    public bool CacheEnabled { get; set; } = true;

    /// <summary>
    /// Registers a configuration provider. Providers registered later have lower priority.
    /// </summary>
    public void AddProvider(IConfigurationProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _rwLock.EnterWriteLock();
        try
        {
            _providers.Add(provider);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Registers a configuration provider at the specified index (0 = highest priority).
    /// </summary>
    public void InsertProvider(int index, IConfigurationProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _rwLock.EnterWriteLock();
        try
        {
            _providers.Insert(index, provider);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets a strongly-typed configuration section from the first provider that has it.
    /// </summary>
    public T? GetSection<T>(string sectionName) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        if (CacheEnabled && _cache.TryGetValue(sectionName, out var cached))
        {
            return cached as T;
        }

        _rwLock.EnterReadLock();
        try
        {
            foreach (var provider in _providers)
            {
                var value = provider.GetSection<T>(sectionName);
                if (value is not null)
                {
                    if (CacheEnabled)
                        _cache[sectionName] = value;
                    return value;
                }
            }
        }
        finally
        {
            _rwLock.ExitReadLock();
        }

        return null;
    }

    /// <summary>
    /// Gets a configuration value from the first provider that has it.
    /// </summary>
    public string? GetValue(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (CacheEnabled && _cache.TryGetValue(key, out var cached))
        {
            return cached as string;
        }

        _rwLock.EnterReadLock();
        try
        {
            foreach (var provider in _providers)
            {
                var value = provider.GetValue(key);
                if (value is not null)
                {
                    if (CacheEnabled)
                        _cache[key] = value;
                    return value;
                }
            }
        }
        finally
        {
            _rwLock.ExitReadLock();
        }

        return null;
    }

    /// <summary>
    /// Attempts to get a configuration value from the first provider that has it.
    /// </summary>
    public bool TryGetValue(string key, out string? value)
    {
        value = GetValue(key);
        return value is not null;
    }

    /// <summary>
    /// Reloads all registered providers and clears the cache.
    /// </summary>
    public void ReloadAll()
    {
        _rwLock.EnterWriteLock();
        try
        {
            _cache.Clear();
            foreach (var provider in _providers)
            {
                provider.Reload();
            }
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears the configuration cache without reloading providers.
    /// </summary>
    public void ClearCache()
    {
        _rwLock.EnterWriteLock();
        try
        {
            _cache.Clear();
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _rwLock.Dispose();
            foreach (var provider in _providers.OfType<IDisposable>())
            {
                provider.Dispose();
            }
            _providers.Clear();
            _cache.Clear();
        }
    }
}

