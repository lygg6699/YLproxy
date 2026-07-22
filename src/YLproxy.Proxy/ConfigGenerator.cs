using System;
using System.Text;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Proxy;

public static class ConfigGenerator
{
    public static string Generate(ProxyItem proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        if (string.IsNullOrWhiteSpace(proxy.RemoteHost)) throw new ArgumentException("RemoteHost is empty");
        if (proxy.RemotePort <= 0) throw new ArgumentException("RemotePort is invalid");
        if (proxy.LocalPort <= 0) throw new ArgumentException("LocalPort is invalid");

        var remoteHost = ValidateToken(proxy.RemoteHost, nameof(proxy.RemoteHost));
        var hasUsername = !string.IsNullOrWhiteSpace(proxy.Username);
        var hasPassword = !string.IsNullOrWhiteSpace(proxy.Password);
        if (hasUsername != hasPassword)
            throw new ArgumentException("Remote proxy username and password must be provided together.");

        var username = hasUsername ? ValidateToken(proxy.Username, nameof(proxy.Username)) : string.Empty;
        var password = hasPassword ? ValidateToken(proxy.Password, nameof(proxy.Password)) : string.Empty;

        var sb = new StringBuilder();

        sb.AppendLine("service");
        sb.AppendLine($"log {GetProxyLogPath(proxy.Id)} D");
        sb.AppendLine("logformat \"- +_L%t.%. %N.%p %E %U %C:%c %R:%r %O %I %h %T\"");
        sb.AppendLine("auth iponly");
        sb.AppendLine("allow *");
        sb.AppendLine("internal 127.0.0.1");
        sb.AppendLine("fakeresolve");

        var parentCredentials = hasUsername ? $" {username} {password}" : string.Empty;
        sb.AppendLine($"parent 1000 http {remoteHost} {proxy.RemotePort}{parentCredentials}");
        sb.AppendLine($"proxy -a -p{proxy.LocalPort}");

        sb.AppendLine("flush");
        return sb.ToString();
    }

    private static string ValidateToken(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace) || value.Any(char.IsControl) || value.Contains('"'))
            throw new ArgumentException($"{parameterName} contains characters that cannot be represented safely in a 3proxy configuration.", parameterName);

        return value;
    }

    /// <summary>
    /// Returns the full log file path for a given proxy.
    /// </summary>
    private static string GetProxyLogPath(int proxyId)
    {
        var runtimePath = GetRuntime3ProxyPath();
        return PathHelper.Combine(runtimePath, "logs", $"3proxy-{proxyId}.log");
    }

    private static string GetRuntime3ProxyPath()
    {
        return ProxyRuntimeConfiguration.Default.GetRuntimeDirectory();
    }
}

