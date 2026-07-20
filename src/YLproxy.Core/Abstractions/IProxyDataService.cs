using YLproxy.Models;

namespace YLproxy.Core.Abstractions;

/// <summary>
/// Interface for managing proxy data operations.
/// </summary>
public interface IProxyDataService
{
    /// <summary>
    /// Loads the proxy configuration from the file system.
    /// </summary>
    AppConfig Load();

    /// <summary>
    /// Saves the proxy configuration to the file system.
    /// </summary>
    void Save(AppConfig config);
}
