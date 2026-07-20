namespace YLproxy.Models.Config;

public static class ConfigDefaults
{
    public const string LogDirectory = "logs";
    public const string DataDirectory = "data";
    public const string ConfigFileName = "config.json";
    public const string ThreeProxyRuntime = "runtime/3proxy";
    public static readonly string[] ValidLogLevels = { "Debug", "Info", "Warn", "Error" };
    public const string DefaultLogLevel = "Info";
    public const int PortRangeStart = 9001;
    public const int PortRangeEnd = 9099;
    public const int CheckIntervalSeconds = 5;
    public const int LogRetentionDays = 30;
}

