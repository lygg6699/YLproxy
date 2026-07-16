using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using YLproxy.Utils;

namespace YLproxy.Infrastructure
{
    public class FileLogger : ILogger
    {
        private readonly string _logDirectory;
        private readonly int _retentionDays;
        private readonly string _minLevel;
        private static readonly object _lock = new object();
        private readonly List<string> _cleanupErrors = new();

        public IReadOnlyList<string> CleanupErrors => _cleanupErrors.AsReadOnly();

        /// <summary>
        /// Matches DPAPI-encrypted credential blobs that may leak into log output.
        /// Pattern: dpapi:v1: followed by Base64 characters.
        /// </summary>
        private static readonly Regex _credentialPattern = new(
            @"dpapi:v1:[A-Za-z0-9+/=]{20,}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public FileLogger(string logDirectory, int retentionDays, string minLevel)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
            _logDirectory = Path.IsPathFullyQualified(logDirectory)
                ? Path.GetFullPath(logDirectory)
                : PathResolver.ResolvePath(logDirectory);
            _retentionDays = retentionDays;
            _minLevel = minLevel.ToUpper();
            
            // Ensure log directory exists
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
            
            // Clean up old logs
            CleanupOldLogs();
        }

        private string GetLogFilePath()
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            return Path.Combine(_logDirectory, $"log_{datePart}.txt");
        }

        private bool IsLogLevelEnabled(string level)
        {
            var levels = new[] { "DEBUG", "INFO", "WARN", "ERROR", "FATAL" };
            int minLevelIndex = Array.IndexOf(levels, _minLevel);
            int levelIndex = Array.IndexOf(levels, level.ToUpper());
            
            return levelIndex >= minLevelIndex;
        }

        private void WriteLog(string level, string message, Exception? exception = null)
        {
            if (!IsLogLevelEnabled(level))
                return;

            // 脱敏：将 dpapi:v1:... 凭据替换为 [REDACTED]
            message = Sanitize(message);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logLine = $"{timestamp} [{level}] {message}";
            
            if (exception != null)
            {
                var exMsg = Sanitize(exception.Message);
                var exStack = Sanitize(exception.StackTrace ?? string.Empty);
                logLine += Environment.NewLine + $"Exception: {exMsg}";
                logLine += Environment.NewLine + $"StackTrace: {exStack}";
            }

            logLine += Environment.NewLine;

            var bytes = Encoding.UTF8.GetBytes(logLine);
            lock (_lock)
            {
                using (var fs = new FileStream(GetLogFilePath(), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }
            }
        }

        /// <summary>
        /// 将日志中的 DPAPI 加密凭据替换为占位符，防止泄露。
        /// </summary>
        private static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return _credentialPattern.Replace(text, "[REDACTED]");
        }

        public void Debug(string message)
        {
            WriteLog("DEBUG", message);
        }

        public void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public void Warn(string message)
        {
            WriteLog("WARN", message);
        }

        public void Error(string message)
        {
            WriteLog("ERROR", message);
        }

        public void Fatal(string message)
        {
            WriteLog("FATAL", message);
        }

        public void Debug(string message, Exception exception)
        {
            WriteLog("DEBUG", message, exception);
        }

        public void Info(string message, Exception exception)
        {
            WriteLog("INFO", message, exception);
        }

        public void Warn(string message, Exception exception)
        {
            WriteLog("WARN", message, exception);
        }

        public void Error(string message, Exception exception)
        {
            WriteLog("ERROR", message, exception);
        }

        public void Fatal(string message, Exception exception)
        {
            WriteLog("FATAL", message, exception);
        }

        public void CleanupOldLogs()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                    return;

                var files = Directory.GetFiles(_logDirectory, "log_*.txt");
                var cutoffDate = DateTime.Now.Date.AddDays(-_retentionDays);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffDate)
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _cleanupErrors.Add($"Failed to clean up log file '{file}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _cleanupErrors.Add($"Log cleanup directory enumeration failed: {ex.Message}");
            }
        }
    }
}