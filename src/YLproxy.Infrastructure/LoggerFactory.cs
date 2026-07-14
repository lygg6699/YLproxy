using System;
using System.Text.Json;

namespace YLproxy.Infrastructure
{
    public class LoggerFactory
    {
        private static ILogger _logger;
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
                            // Try to load configuration from AppSettings.json
                            string configPath = "AppSettings.json";
                            if (File.Exists(configPath))
                            {
                                string json = File.ReadAllText(configPath);
                                using JsonDocument document = JsonDocument.Parse(json);
                                
                                if (document.RootElement.TryGetProperty("Logging", out var loggingElement))
                                {
                                    string logDirectory = loggingElement.GetProperty("LogDirectory").GetString() ?? "logs";
                                    int retentionDays = loggingElement.GetProperty("RetentionDays").GetInt32();
                                    string minLevel = loggingElement.GetProperty("MinLevel").GetString() ?? "Info";
                                    
                                    _logger = new FileLogger(logDirectory, retentionDays, minLevel);
                                }
                                else
                                {
                                    _logger = new FileLogger("logs", 30, "Info");
                                }
                            }
                            else
                            {
                                _logger = new FileLogger("logs", 30, "Info");
                            }
                        }
                        catch
                        {
                            // Fallback to default logger if configuration fails
                            _logger = new FileLogger("logs", 30, "Info");
                        }
                    }
                }
            }
            
            return _logger;
        }
    }
}