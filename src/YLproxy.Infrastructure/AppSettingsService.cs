using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Infrastructure
{
    public class AppSettingsService
    {
        private readonly string _configFilePath;
        private AppSettingsConfig _config = new AppSettingsConfig();
        private readonly FileSystemWatcher _watcher;
        private readonly List<string> _loadErrors = new();
        private readonly List<string> _saveErrors = new();

        public IReadOnlyList<string> LoadErrors => _loadErrors.AsReadOnly();
        public IReadOnlyList<string> SaveErrors => _saveErrors.AsReadOnly();

        public AppSettingsService(string configFilePath = "AppSettings.json")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configFilePath);
            _configFilePath = Path.IsPathFullyQualified(configFilePath)
                ? Path.GetFullPath(configFilePath)
                : PathResolver.ResolvePath(configFilePath);

            var canonicalPath = PathResolver.ResolvePath("AppSettings.json");
            if (!string.Equals(_configFilePath, canonicalPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Global settings must be stored in the repository AppSettings.json file.", nameof(configFilePath));

            var configDirectory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrWhiteSpace(configDirectory))
                Directory.CreateDirectory(configDirectory);

            LoadConfig();

            _watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(_configFilePath) ?? PathResolver.GetRepositoryRoot(),
                Filter = Path.GetFileName(_configFilePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };

            _watcher.Changed += OnConfigChanged;
            _watcher.Created += OnConfigChanged;
            _watcher.Renamed += OnConfigRenamed;
            _watcher.EnableRaisingEvents = true;
        }

        public T GetSection<T>(string sectionName) where T : class, new()
        {
            return sectionName switch
            {
                "Logging" => _config.Logging as T ?? new T(),
                "Proxy" => _config.Proxy as T ?? new T(),
                "ThreeProxy" => _config.ThreeProxy as T ?? new T(),
                _ => new T()
            };
        }

        public void Reload()
        {
            LoadConfig();
        }

        public AppSettingsConfig GetConfig()
        {
            return _config;
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = SimpleRetry.Execute(() => File.ReadAllText(_configFilePath), maxAttempts: 3, delayMs: 100);
                    var config = JsonSerializer.Deserialize<AppSettingsConfig>(json) ?? new AppSettingsConfig();
                    Validate(config);
                    _config = config;
                }
                else
                {
                    _config = new AppSettingsConfig();
                    Validate(_config);
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                _loadErrors.Add($"Failed to load config: {ex.Message}");
                _config = new AppSettingsConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                _saveErrors.Add($"Failed to save config: {ex.Message}");
            }
        }

        private void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            LoadConfig();
        }

        private void OnConfigRenamed(object sender, RenamedEventArgs e)
        {
            LoadConfig();
        }

        private static void Validate(AppSettingsConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            if (config.Logging is null || string.IsNullOrWhiteSpace(config.Logging.LogDirectory) ||
                !string.Equals(config.Logging.LogDirectory.Replace('\\', '/').Trim('/'), "logs", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Logging.LogDirectory must be the repository logs directory.");

            if (config.Logging.RetentionDays < 0)
                throw new InvalidDataException("Logging.RetentionDays must be zero or greater.");

            if (!new[] { "Debug", "Info", "Warn", "Error" }
                    .Contains(config.Logging.MinLevel, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Logging.MinLevel must be Debug, Info, Warn, or Error.");

            if (config.Proxy is null || config.Proxy.PortRangeStart < 1 ||
                config.Proxy.PortRangeEnd < config.Proxy.PortRangeStart || config.Proxy.PortRangeEnd > 65535 ||
                config.Proxy.CheckIntervalSeconds < 1)
                throw new InvalidDataException("Proxy port range or check interval is invalid.");

            if (!string.Equals(config.Proxy.DataDirectory.Replace('\\', '/').Trim('/'), "data", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(config.Proxy.ConfigFileName) ||
                !string.Equals(Path.GetFileName(config.Proxy.ConfigFileName), config.Proxy.ConfigFileName, StringComparison.Ordinal) ||
                !string.Equals(config.Proxy.ConfigFileName, "config.json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Proxy data must be stored in data/config.json.");

            if (config.ThreeProxy is null ||
                !string.Equals(config.ThreeProxy.RuntimeDirectory.Replace('\\', '/').Trim('/'), "runtime/3proxy", StringComparison.OrdinalIgnoreCase) ||
                config.ThreeProxy.RequiredDlls is null ||
                config.ThreeProxy.RequiredDlls.Any(dll => string.IsNullOrWhiteSpace(dll) ||
                    !string.Equals(Path.GetFileName(dll), dll, StringComparison.Ordinal)))
                throw new InvalidDataException("3proxy runtime or DLL configuration is invalid.");
        }
    }

    public class AppSettingsConfig
    {
        public LoggingConfig Logging { get; set; } = new LoggingConfig();
        public ProxyConfig Proxy { get; set; } = new ProxyConfig();
        public ThreeProxyConfig ThreeProxy { get; set; } = new ThreeProxyConfig();
        public StartupConfig Startup { get; set; } = new StartupConfig();
    }

    public class LoggingConfig
    {
        public string LogDirectory { get; set; } = "logs";
        public int RetentionDays { get; set; } = 30;
        public string MinLevel { get; set; } = "Info";
    }

    public class ProxyConfig
    {
        public string DataDirectory { get; set; } = "data";
        public string ConfigFileName { get; set; } = "config.json";
        public int PortRangeStart { get; set; } = 9001;
        public int PortRangeEnd { get; set; } = 9100;
        public int CheckIntervalSeconds { get; set; } = 5;
    }

    public class ThreeProxyConfig
    {
        public string RuntimeDirectory { get; set; } = "runtime/3proxy";
        public List<string> RequiredDlls { get; set; } = new List<string> { "FilePlugin.dll", "StringsPlugin.dll" };
    }

    public class StartupConfig
    {
        public bool AutoStart { get; set; }
    }
}