using System.Net;
using System.Net.Sockets;

namespace YLproxy.Utils;

public static class NetworkUtil
{
    /// <summary>
    /// Returns the first non-loopback IPv4 address of the local machine, or null.
    /// </summary>
    public static string? GetBestLocalIp()
    {
        try
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    return ip.ToString();
            }
        }
        catch
        {
            // swallow — caller handles null
        }

        return null;
    }
}
