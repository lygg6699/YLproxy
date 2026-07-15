using System;
using System.Text;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Proxy;

public static class ConfigGenerator
{
    public static string Generate(ProxyItem proxy)
    {
        if (proxy is null) throw new ArgumentNullException(nameof(proxy));
        if (string.IsNullOrWhiteSpace(proxy.RemoteHost)) throw new ArgumentException("RemoteHost is empty");
        if (proxy.RemotePort <= 0) throw new ArgumentException("RemotePort is invalid");
        if (proxy.LocalPort <= 0) throw new ArgumentException("LocalPort is invalid");

        var sb = new StringBuilder();
        var runtimePath = GetRuntime3ProxyPath();

        sb.AppendLine("service");
        sb.AppendLine($"log {runtimePath}\\logs\\3proxy-{proxy.Id}.log D");
        sb.AppendLine("logformat \"- +_L%t.%. %N.%p %E %U %C:%c %R:%r %O %I %h %T\"");
        sb.AppendLine("auth iponly");
        sb.AppendLine("allow *");
        sb.AppendLine("internal 127.0.0.1");

        if (!string.IsNullOrWhiteSpace(proxy.Username) && !string.IsNullOrWhiteSpace(proxy.Password))
        {
            sb.AppendLine($"proxy -a -p{proxy.LocalPort} -e{proxy.RemoteHost}:{proxy.RemotePort} -u{proxy.Username} -k{proxy.Password}");
        }
        else
        {
            sb.AppendLine($"proxy -a -p{proxy.LocalPort} -e{proxy.RemoteHost}:{proxy.RemotePort}");
        }

        sb.AppendLine("flush");
        return sb.ToString();
    }

    private static string GetRuntime3ProxyPath()
    {
        return ProxyRuntimeConfiguration.GetRuntimeDirectory();
    }
}

