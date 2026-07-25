using System.Collections.Generic;

namespace YLproxy.Models;

public sealed class AppConfig
{
    /// <summary>
    /// 配置架构版本号。当前版本: "1.1"
    /// - null/空: 旧版未加密配置（需要迁移）
    /// - "1.0": 已加密但无版本标记
    /// - "1.1": 当前版本（已加密 + 版本标记）
    /// </summary>
    public string? Version { get; set; }

    public List<ProxyItem> Proxies { get; set; } = new();

    /// <summary>
    /// 当前配置架构版本号。
    /// </summary>
    public const string CurrentVersion = "1.1";
}

