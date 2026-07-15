using System;
using System.Collections.Generic;
using System.Linq;
using YLproxy.Utils;

namespace YLproxy.Proxy;

internal static class ProxyRuntimeConfiguration
{
    private static string _runtimeDirectory = "runtime/3proxy";
    private static IReadOnlyList<string> _requiredDlls = new[]
    {
        "FilePlugin.dll",
        "StringsPlugin.dll"
    };

    public static void Configure(string runtimeDirectory, IEnumerable<string> requiredDlls)
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

    public static string GetRuntimeDirectory()
    {
        return PathResolver.ResolvePath(_runtimeDirectory);
    }

    public static IReadOnlyList<string> GetRequiredDlls()
    {
        return _requiredDlls;
    }
}