using System;
using System.Collections.Generic;
using System.Linq;
using YLproxy.Models.Config;
using YLproxy.Utils;

namespace YLproxy.Proxy;

public sealed class ProxyRuntimeConfiguration
{
    private string _runtimeDirectory = "runtime/3proxy";
    private IReadOnlyList<string> _requiredDlls = new[]
    {
        "FilePlugin.dll",
        "StringsPlugin.dll"
    };

    /// <summary>
    /// 默认全局实例，用于向后兼容静态调用。
    /// </summary>
    internal static readonly ProxyRuntimeConfiguration Default = new();

    public ProxyRuntimeConfiguration()
    {
    }

    public ProxyRuntimeConfiguration(string? runtimeDirectory, IEnumerable<string>? requiredDlls)
    {
        Configure(runtimeDirectory, requiredDlls);
    }

    public void Configure(string? runtimeDirectory, IEnumerable<string>? requiredDlls)
    {
        if (!string.IsNullOrWhiteSpace(runtimeDirectory))
            _runtimeDirectory = runtimeDirectory.Trim();

        var configuredDlls = requiredDlls?
            .Where(dll => !string.IsNullOrWhiteSpace(dll))
            .Select(dll => dll.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (configuredDlls is { Length: > 0 })
            _requiredDlls = configuredDlls;
    }

    public string GetRuntimeDirectory()
    {
        return PathResolver.ResolvePath(_runtimeDirectory);
    }

    public IReadOnlyList<string> GetRequiredDlls()
    {
        return _requiredDlls;
    }
}
