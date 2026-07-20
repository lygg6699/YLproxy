namespace YLproxy.Models.Config;

public class ProxyConfig
{
    public string DataDirectory { get; set; } = ConfigDefaults.DataDirectory;
    public string ConfigFileName { get; set; } = ConfigDefaults.ConfigFileName;
    public int PortRangeStart { get; set; } = ConfigDefaults.PortRangeStart;
    public int PortRangeEnd { get; set; } = ConfigDefaults.PortRangeEnd;
    public int CheckIntervalSeconds { get; set; } = ConfigDefaults.CheckIntervalSeconds;
}

