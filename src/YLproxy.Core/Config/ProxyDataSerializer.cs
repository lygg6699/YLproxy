using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using YLproxy.Infrastructure;
using YLproxy.Models;

namespace YLproxy.Core.Config;

public sealed class ProxyDataSerializer
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
    };

    private readonly ISecurityService _securityService;

    public ProxyDataSerializer(ISecurityService? securityService = null)
    {
        if (securityService is null)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("DPAPI credential storage requires Windows platform.");
            
            _securityService = new DpapiSecurityService();
        }
        else
        {
            _securityService = securityService;
        }
    }

    public AppConfig Deserialize(string json, out bool requiresMigration)
    {
        ArgumentNullException.ThrowIfNull(json);

        var storedConfig = JsonSerializer.Deserialize<StoredAppConfig>(json, _jsonOptions) ?? new StoredAppConfig();
        var config = new AppConfig();
        requiresMigration = false;

        foreach (var storedProxy in storedConfig.Proxies ?? Enumerable.Empty<StoredProxyItem>())
        {
            var username = storedProxy.Username ?? string.Empty;
            var password = storedProxy.Password ?? string.Empty;

            if ((!string.IsNullOrEmpty(username) && !_securityService.IsEncrypted(username)) ||
                (!string.IsNullOrEmpty(password) && !_securityService.IsEncrypted(password)))
            {
                requiresMigration = true;
            }

            config.Proxies.Add(new ProxyItem
            {
                Id = storedProxy.Id,
                Name = storedProxy.Name ?? string.Empty,
                RemoteHost = storedProxy.RemoteHost ?? string.Empty,
                RemotePort = storedProxy.RemotePort,
                Username = _securityService.Decrypt(username),
                Password = _securityService.Decrypt(password),
                LocalHost = storedProxy.LocalHost ?? string.Empty,
                LocalPort = storedProxy.LocalPort,
                Status = storedProxy.Status,
                CreateTime = storedProxy.CreateTime,
                Group = storedProxy.Group ?? string.Empty,
            });
        }

        return config;
    }

    public string Serialize(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var storedConfig = new StoredAppConfig
        {
            Proxies = config.Proxies.Select(proxy => new StoredProxyItem
            {
                Id = proxy.Id,
                Name = proxy.Name,
                RemoteHost = proxy.RemoteHost,
                RemotePort = proxy.RemotePort,
                Username = _securityService.Encrypt(proxy.Username),
                Password = _securityService.Encrypt(proxy.Password),
                LocalHost = proxy.LocalHost,
                LocalPort = proxy.LocalPort,
                Status = proxy.Status,
                CreateTime = proxy.CreateTime,
                Group = proxy.Group,
            }).ToList(),
        };

        return JsonSerializer.Serialize(storedConfig, _jsonOptions);
    }

    private sealed class StoredAppConfig
    {
        public List<StoredProxyItem> Proxies { get; set; } = new();
    }

    private sealed class StoredProxyItem
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? RemoteHost { get; set; }
        public int RemotePort { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? LocalHost { get; set; }
        public int LocalPort { get; set; }
        public ProxyStatus Status { get; set; }
        public DateTime CreateTime { get; set; }
        public string? Group { get; set; }
    }
}
