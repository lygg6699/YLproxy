using System;
using System.Text.Json;

namespace YLproxy.Infrastructure
{
    public class LoggerFactory
    {
        private static ILogger? _logger;
        private static readonly object _lock = new object();
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public static ILogger CreateLogger()
        {
            if (_logger is not null)
                return _logger;

            lock (_lock)
            {
                if (_logger is not null)
                    return _logger;

                // Read config directly to avoid circular dependency with AppSettingsService.
                try
                {
                    var configPath = Utils.PathResolver.ResolvePath("AppSettings.json");
                    if (System.IO.File.Exists(configPath))
                    {
                        var json = System.IO.File.ReadAllText(configPath);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("Logging", out var loggingEl))
                        {
                            var config = JsonSerializer.Deserialize<LoggingConfig>(
                                loggingEl.GetRawText(), JsonOptions);
                            if (config is not null)
                            {
                                _logger = new FileLogger(
                                    config.LogDirectory,
                                    config.RetentionDays,
                                    config.MinLevel);
                                return _logger;
                            }
                        }
                    }
                }
                catch
                {
                    // Use defaults if reading config fails.
                }

                _logger = new FileLogger("logs", 30, "Info");
                return _logger;
            }
        }
    }
}
