using System;
namespace YLproxy.Infrastructure
{
    public class LoggerFactory
    {
        private static ILogger? _logger;
        private static readonly object _lock = new object();

        public static ILogger CreateLogger()
        {
            if (_logger == null)
            {
                lock (_lock)
                {
                    if (_logger == null)
                    {
                        try
                        {
                            var settings = new AppSettingsService().GetSection<LoggingConfig>("Logging");
                            _logger = new FileLogger(
                                settings.LogDirectory,
                                settings.RetentionDays,
                                settings.MinLevel);
                        }
                        catch
                        {
                            // Fallback to default logger if configuration fails
                            _logger = new FileLogger("logs", 30, "Info");
                        }
                    }
                }
            }
            
            return _logger!;
        }
    }
}
