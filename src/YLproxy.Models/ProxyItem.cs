using System;

namespace YLproxy.Models;

public sealed class ProxyItem
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string RemoteHost { get; init; } = string.Empty;
    public int RemotePort { get; init; }

    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public string LocalHost { get; init; } = string.Empty;
    public int LocalPort { get; init; }

    public ProxyStatus Status { get; set; } = ProxyStatus.Stopped;

    public DateTime CreateTime { get; init; } = DateTime.UtcNow;
}

