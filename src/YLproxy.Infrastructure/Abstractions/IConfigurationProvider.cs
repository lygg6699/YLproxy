namespace YLproxy.Infrastructure.Abstractions;

/// <summary>
/// Defines a contract for configuration providers that support multiple configuration sources
/// (JSON files, environment variables, etc.) with a unified access pattern.
/// </summary>
public interface IConfigurationProvider
{
    /// <summary>
    /// Gets a strongly-typed configuration section.
    /// </summary>
    /// <typeparam name="T">The type of the configuration section.</typeparam>
    /// <param name="sectionName">The name of the configuration section.</param>
    /// <returns>The deserialized configuration section, or default if not found.</returns>
    T? GetSection<T>(string sectionName) where T : class;

    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value, or null if not found.</returns>
    string? GetValue(string key);

    /// <summary>
    /// Attempts to get a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">When this method returns, contains the value if found.</param>
    /// <returns>true if the key was found; otherwise, false.</returns>
    bool TryGetValue(string key, out string? value);

    /// <summary>
    /// Reloads the configuration from its source.
    /// </summary>
    void Reload();

    /// <summary>
    /// Gets the name of this configuration provider (for diagnostics).
    /// </summary>
    string Name { get; }
}

