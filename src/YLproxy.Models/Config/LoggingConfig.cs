namespace YLproxy.Models.Config;

public class LoggingConfig
{
    public string LogDirectory { get; set; } = ConfigDefaults.LogDirectory;
    public int RetentionDays { get; set; } = ConfigDefaults.LogRetentionDays;
    public string MinLevel { get; set; } = ConfigDefaults.DefaultLogLevel;
}

