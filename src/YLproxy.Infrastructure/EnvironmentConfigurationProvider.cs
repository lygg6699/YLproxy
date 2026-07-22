using YLproxy.Infrastructure.Abstractions;

namespace YLproxy.Infrastructure;

/// <summary>
/// Configuration provider that reads settings from environment variables.
/// Supports nested keys using double underscore (__) separator,
/// e.g., "Logging__LogLevel" maps to Logging:LogLevel.
/// </summary>
public sealed class EnvironmentConfigurationProvider : IConfigurationProvider
{
    /// <inheritdoc />
    public string Name => "EnvironmentVariables";

    /// <inheritdoc />
    public T? GetSection<T>(string sectionName) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        var prefix = sectionName.Replace(":", "__").Replace(".", "__") + "__";
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var envVar in Environment.GetEnvironmentVariables())
        {
            if (envVar is System.Collections.DictionaryEntry entry &&
                entry.Key is string key &&
                entry.Value is string value &&
                key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var configKey = key[prefix.Length..].Replace("__", ":");
                values[configKey] = value;
            }
        }

        if (values.Count == 0)
            return null;

        // Parse the section from flat key-value pairs
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return config.Get<T>();
    }

    /// <inheritdoc />
    public string? GetValue(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // Environment variables use __ as separator instead of :
        var envKey = key.Replace(":", "__").Replace(".", "__");
        return Environment.GetEnvironmentVariable(envKey);
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
        // Environment variables are always read fresh from the OS,
        // so Reload() is a no-op.
    }
}

