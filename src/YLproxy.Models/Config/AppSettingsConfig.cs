namespace YLproxy.Models.Config;

public class AppSettingsConfig
{
    public LoggingConfig Logging { get; set; } = new LoggingConfig();
    public ProxyConfig Proxy { get; set; } = new ProxyConfig();
    public ThreeProxyConfig ThreeProxy { get; set; } = new ThreeProxyConfig();
    public ApiConfig Api { get; set; } = new ApiConfig();
    public StartupConfig Startup { get; set; } = new StartupConfig();
}

