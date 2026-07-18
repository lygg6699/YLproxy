using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using YLproxy.Utils;

namespace YLproxy.Infrastructure
{
    public class FileLogger : ILogger, IDisposable
    {
        private readonly string _logDirectory;
        private readonly int _retentionDays;
        private readonly string _minLevel;
        private static readonly object _lock = new object();
        private readonly List<string> _cleanupErrors = new();
        private int _disposed;

        public IReadOnlyList<string> CleanupErrors => _cleanupErrors.AsReadOnly();

        /// <summary>
        /// Matches DPAPI-encrypted credential blobs that may leak into log output.
        /// Pattern: dpapi:v1: followed by Base64 characters.
        /// </summary>
        private static readonly Regex _credentialPattern = new(
            @"dpapi:v1:[A-Za-z0-9+/=]{20,}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        public FileLogger(string logDirectory, int retentionDays, string minLevel)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
            _logDirectory = Path.IsPathFullyQualified(logDirectory)
                ? Path.GetFullPath(logDirectory)
                : PathResolver.ResolvePath(logDirectory);
            _retentionDays = retentionDays;
            _minLevel = minLevel.ToUpper(CultureInfo.InvariantCulture);

            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            CleanupOldLogs();
        }

        private string GetLogFilePath()
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return Path.Combine(_logDirectory, $"log_{datePart}.txt");
        }

        private bool IsLogLevelEnabled(string level)
        {
            var levels = new[] { "DEBUG", "INFO", "WARN", "ERROR", "FATAL" };
            int minLevelIndex = Array.IndexOf(levels, _minLevel);
            int levelIndex = Array.IndexOf(levels, level.ToUpper(CultureInfo.InvariantCulture));
            return levelIndex >= minLevelIndex;
        }

        private static int LevelToOrdinal(string level) => level.ToUpper(CultureInfo.InvariantCulture) switch
        {
            "DEBUG" => (int)LogLevel.Debug,
            "INFO" => (int)LogLevel.Info,
            "WARN" => (int)LogLevel.Warn,
            "ERROR" => (int)LogLevel.Error,
            "FATAL" => (int)LogLevel.Fatal,
            _ => (int)LogLevel.Info,
        };

        public void Log(LogLevel level, string message, object? data = null)
        {
            WriteStructuredLog(level, Sanitize(message), correlationId: null, exception: null, data: data);
        }

        public void Log(LogLevel level, string message, Exception exception, object? data = null)
        {
            WriteStructuredLog(level, Sanitize(message), correlationId: null, exception: exception, data: data);
        }

        private void WriteStructuredLog(LogLevel level, string message, string? correlationId, Exception? exception, object? data)
        {
            var levelStr = level.ToString().ToUpper(CultureInfo.InvariantCulture);
            if (!IsLogLevelEnabled(levelStr))
                return;

            var entry = new StructuredLogEntry
            {
                Timestamp = DateTime.UtcNow.ToString("O"),
                Level = levelStr,
                Message = message,
                CorrelationId = correlationId,
                ExceptionMessage = exception?.Message is not null ? Sanitize(exception.Message) : null,
                ExceptionType = exception?.GetType().FullName,
                StackTrace = exception?.StackTrace is not null ? Sanitize(exception.StackTrace) : null,
            };

            if (data is not null)
            {
                entry = entry with { Data = new Dictionary<string, object?> { ["value"] = data } };
            }

            string jsonLine;
            try
            {
                jsonLine = JsonSerializer.Serialize(entry, _jsonOptions) + Environment.NewLine;
            }
            catch
            {
                jsonLine = $"{entry.Timestamp} [{levelStr}] {entry.Message}{Environment.NewLine}";
            }

            WriteLine(jsonLine);
        }

        private void WriteLine(string line)
        {
            var targetFile = GetLogFilePath();
            lock (_lock)
            {
                try
                {
                    using var fs = new FileStream(targetFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(fs, Encoding.UTF8);
                    writer.Write(line);
                    writer.Flush();
                }
                catch (IOException)
                {
                    // Log file is locked, discard entry without blocking.
                }
            }
        }

        private void WriteLog(string level, string message, Exception? exception = null)
        {
            var ordinal = LevelToOrdinal(level);
            if (exception is not null)
                Log((LogLevel)ordinal, message, exception);
            else
                Log((LogLevel)ordinal, message);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            GC.SuppressFinalize(this);
        }

        private static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return _credentialPattern.Replace(text, "[REDACTED]");
        }

        public void Debug(string message) => WriteLog("DEBUG", message);
        public void Info(string message) => WriteLog("INFO", message);
        public void Warn(string message) => WriteLog("WARN", message);
        public void Error(string message) => WriteLog("ERROR", message);
        public void Fatal(string message) => WriteLog("FATAL", message);
        public void Debug(string message, Exception exception) => WriteLog("DEBUG", message, exception);
        public void Info(string message, Exception exception) => WriteLog("INFO", message, exception);
        public void Warn(string message, Exception exception) => WriteLog("WARN", message, exception);
        public void Error(string message, Exception exception) => WriteLog("ERROR", message, exception);
        public void Fatal(string message, Exception exception) => WriteLog("FATAL", message, exception);

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
