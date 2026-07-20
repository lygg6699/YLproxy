using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using YLproxy.Models;
using YLproxy.Models.Config;
using YLproxy.Utils;
using YLproxy.Infrastructure.Abstractions;

namespace YLproxy.Infrastructure
{

    public class AppSettingsService : IAppSettingsService
    {

        private readonly string _configFilePath;
        private AppSettingsConfig _config = new AppSettingsConfig();
        private readonly FileSystemWatcher _watcher;
        private readonly List<string> _loadErrors = new();
        private readonly List<string> _saveErrors = new();
        private DateTime _lastConfigLoad = DateTime.MinValue;
        private static readonly TimeSpan ConfigLoadDebounce = TimeSpan.FromMilliseconds(500);
        private bool _isSaving = false;

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

        /// <summary>
        /// Gets the logging configuration section.
        /// </summary>
        public LoggingConfig GetLoggingConfig() => _config.Logging;

        /// <summary>
        /// Gets the proxy configuration section.
        /// </summary>
        public ProxyConfig GetProxyConfig() => _config.Proxy;

        /// <summary>
        /// Gets the ThreeProxy configuration section.
        /// </summary>
        public ThreeProxyConfig GetThreeProxyConfig() => _config.ThreeProxy;

        /// <summary>
        /// Gets the API configuration section.
        /// </summary>
        public ApiConfig GetApiConfig() => _config.Api;

        /// <summary>
        /// Gets a configuration section by name. Prefer the strongly-typed Get*Config() methods instead.
        /// </summary>
        [Obsolete("Use GetLoggingConfig(), GetProxyConfig(), GetThreeProxyConfig(), or GetApiConfig() instead")]
        public T GetSection<T>(string sectionName) where T : class, new()
        {
            return sectionName switch
            {
                "Logging" => _config.Logging as T ?? new T(),
                "Proxy" => _config.Proxy as T ?? new T(),
                "ThreeProxy" => _config.ThreeProxy as T ?? new T(),
                "Api" => _config.Api as T ?? new T(),
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

                EnsureApiToken();
            }
            catch (Exception ex)
            {
                _loadErrors.Add($"Failed to load config: {ex.Message}");
                _config = new AppSettingsConfig();
            }
        }

        private void EnsureApiToken()
        {
            const string defaultToken = "ylproxy-api-token-change-me-in-production";
            if (string.IsNullOrWhiteSpace(_config.Api.AccessToken) ||
                string.Equals(_config.Api.AccessToken, defaultToken, StringComparison.Ordinal))
            {
                var tokenBytes = new byte[24];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(tokenBytes);
                _config.Api.AccessToken = "ylpx-" + Convert.ToBase64String(tokenBytes).Replace("+", "").Replace("/", "").Replace("=", "");
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            _isSaving = true;
            try
            {
                var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                var tempPath = _configFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    var dir = Path.GetDirectoryName(_configFilePath);
                    if (!string.IsNullOrWhiteSpace(dir))
                        Directory.CreateDirectory(dir);
                    using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(fs, System.Text.Encoding.UTF8))
                    {
                        writer.Write(json);
                    }
                    File.Move(tempPath, _configFilePath, true);
                }
                finally
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                    catch (Exception ex)
                    {
                        // Non-critical: temp file cleanup failure should not affect config save result
                        _saveErrors.Add($"Failed to delete temp file: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _saveErrors.Add($"Failed to save config: {ex.Message}");
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            if (_isSaving)
                return;

            var now = DateTime.UtcNow;
            if (now - _lastConfigLoad < ConfigLoadDebounce)
                return;

            LoadConfig();
            _lastConfigLoad = DateTime.UtcNow;
        }

        private void OnConfigRenamed(object sender, RenamedEventArgs e)
        {
            if (_isSaving)
                return;

            var now = DateTime.UtcNow;
            if (now - _lastConfigLoad < ConfigLoadDebounce)
                return;

            LoadConfig();
            _lastConfigLoad = DateTime.UtcNow;
        }

private static void Validate(AppSettingsConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            if (config.Logging is null || string.IsNullOrWhiteSpace(config.Logging.LogDirectory))
            {
                throw new InvalidDataException("Logging.LogDirectory is required.");
            }

            // Relaxed: log directory can be any valid path, not just "logs"
            // Only validate format, not the specific directory name
            var logDir = config.Logging.LogDirectory.Replace('\\', '/').Trim('/');
            if (logDir.Contains("..") || logDir.Contains("~"))
            {
                throw new InvalidDataException("Logging.LogDirectory must not contain relative path segments.");
            }

            if (config.Logging.RetentionDays < 0)
                throw new InvalidDataException("Logging.RetentionDays must be zero or greater.");

            if (!ConfigDefaults.ValidLogLevels
                    .Contains(config.Logging.MinLevel, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Logging.MinLevel must be Debug, Info, Warn, or Error.");

            if (config.Proxy is null || config.Proxy.PortRangeStart < 1 ||
                config.Proxy.PortRangeEnd < config.Proxy.PortRangeStart || config.Proxy.PortRangeEnd > 65535 ||
                config.Proxy.CheckIntervalSeconds < 1)
                throw new InvalidDataException("Proxy port range or check interval is invalid.");

            if (!string.Equals(config.Proxy.DataDirectory.Replace('\\', '/').Trim('/'), ConfigDefaults.DataDirectory, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(config.Proxy.ConfigFileName) ||
                !string.Equals(Path.GetFileName(config.Proxy.ConfigFileName), config.Proxy.ConfigFileName, StringComparison.Ordinal) ||
                !string.Equals(config.Proxy.ConfigFileName, ConfigDefaults.ConfigFileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Proxy data must be stored in data/config.json.");

            if (config.ThreeProxy is null ||
                !string.Equals(config.ThreeProxy.RuntimeDirectory.Replace('\\', '/').Trim('/'), ConfigDefaults.ThreeProxyRuntime, StringComparison.OrdinalIgnoreCase) ||
                config.ThreeProxy.RequiredDlls is null ||
                config.ThreeProxy.RequiredDlls.Any(dll => string.IsNullOrWhiteSpace(dll) ||
                    !string.Equals(Path.GetFileName(dll), dll, StringComparison.Ordinal)))
                throw new InvalidDataException("3proxy runtime or DLL configuration is invalid.");
        }
    }
}
