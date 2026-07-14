using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using YLproxy.Models;

namespace YLproxy.Infrastructure
{
    public class ConfigService : IConfigService
    {
        private readonly string _configFilePath;
        private AppConfig _config;
        private readonly FileSystemWatcher _watcher;

        public ConfigService(string configFilePath = "AppSettings.json")
        {
            _configFilePath = configFilePath;
            LoadConfig();
            
            // Set up file watcher for automatic reload
            _watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(_configFilePath) ?? ".",
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
            if (_config == null)
                return new T();

            return sectionName switch
            {
                "Logging" => _config.Logging as T ?? new T() as T,
                "Proxy" => _config.Proxy as T ?? new T() as T,
                "Application" => _config.Application as T ?? new T() as T,
                "ThreeProxy" => _config.ThreeProxy as T ?? new T() as T,
                _ => new T()
            };
        }

        public void UpdateSection<T>(string sectionName, T settings) where T : class
        {
            if (_config == null)
                _config = new AppConfig();

            switch (sectionName)
            {
                case "Logging":
                    if (settings is LoggingConfig loggingConfig)
                        _config.Logging = loggingConfig;
                    break;
                case "Proxy":
                    if (settings is ProxyConfig proxyConfig)
                        _config.Proxy = proxyConfig;
                    break;
                case "Application":
                    if (settings is ApplicationConfig applicationConfig)
                        _config.Application = applicationConfig;
                    break;
                case "ThreeProxy":
                    if (settings is ThreeProxyConfig threeProxyConfig)
                        _config.ThreeProxy = threeProxyConfig;
                    break;
            }

            SaveConfig();
        }

        public void Reload()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    _config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                else
                {
                    _config = new AppConfig();
                    SaveConfig(); // Create default config file
                }
            }
            catch (Exception)
            {
                _config = new AppConfig(); // Fallback to default config
            }
        }

        private void SaveConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception)
            {
                // Handle save error appropriately in a real application
            }
        }

        private void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce rapid file changes
            LoadConfig();
        }

        private void OnConfigRenamed(object sender, RenamedEventArgs e)
        {
            LoadConfig();
        }
    }

    // Configuration classes matching the AppSettings.json structure
    public class AppConfig
    {
        public LoggingConfig Logging { get; set; } = new LoggingConfig();
        public ProxyConfig Proxy { get; set; } = new ProxyConfig();
        public ApplicationConfig Application { get; set; } = new ApplicationConfig();
        public ThreeProxyConfig ThreeProxy { get; set; } = new ThreeProxyConfig();
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

    public class ApplicationConfig
    {
        public string Theme { get; set; } = "Light";
        public bool AutoStart { get; set; } = false;
        public string UpdateUrl { get; set; } = "";
        public bool StartupMinimized { get; set; } = false;
    }

    public class ThreeProxyConfig
    {
        public string RuntimeDirectory { get; set; } = "runtime/3proxy";
        public List<string> RequiredDlls { get; set; } = new List<string> { "FilePlugin.dll", "StringsPlugin.dll" };
    }
}
