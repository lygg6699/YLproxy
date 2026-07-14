using System.Collections.Generic;

namespace YLproxy.Models;

public sealed class AppConfig
{
    public List<ProxyItem> Proxies { get; set; } = new();
}

