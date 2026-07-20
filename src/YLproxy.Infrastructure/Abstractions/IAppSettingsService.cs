using YLproxy.Models.Config;

namespace YLproxy.Infrastructure.Abstractions;

public interface IAppSettingsService
{
    /// <summary>
    /// Gets a configuration section by name. Prefer the strongly-typed Get*Config() methods.
    /// </summary>
    [Obsolete("Use GetLoggingConfig(), GetProxyConfig(), GetThreeProxyConfig(), or GetApiConfig() instead")]
    T GetSection<T>(string sectionName) where T : class, new();

    /// <summary>Gets the logging configuration section.</summary>
    LoggingConfig GetLoggingConfig();

    /// <summary>Gets the proxy configuration section.</summary>
    ProxyConfig GetProxyConfig();

    /// <summary>Gets the ThreeProxy configuration section.</summary>
    ThreeProxyConfig GetThreeProxyConfig();

    /// <summary>Gets the API configuration section.</summary>
    ApiConfig GetApiConfig();

    AppSettingsConfig GetConfig();
    void Reload();
}

