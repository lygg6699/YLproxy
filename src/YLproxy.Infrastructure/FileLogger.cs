using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YLproxy.Utils;

namespace YLproxy.Infrastructure
{
    public class FileLogger : ILogger
    {
        private readonly string _logDirectory;
        private readonly int _retentionDays;
        private readonly string _minLevel;
        private static readonly object _lock = new object();

        public FileLogger(string logDirectory, int retentionDays, string minLevel)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
            _logDirectory = Path.IsPathFullyQualified(logDirectory)
                ? Path.GetFullPath(logDirectory)
                : PathResolver.ResolvePath(logDirectory);
            _retentionDays = retentionDays;
            _minLevel = minLevel.ToUpperInvariant();

            // Ensure log directory exists
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            _ = Task.Run(CleanupOldLogs);
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
            int levelIndex = Array.IndexOf(levels, level.ToUpperInvariant());

            return levelIndex >= minLevelIndex;
        }

        private void WriteLog(string level, string message, Exception? exception = null)
        {
            if (!IsLogLevelEnabled(level))
                return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string logLine = $"{timestamp} [{level}] {message}";

            if (exception != null)
            {
                logLine += Environment.NewLine + $"Exception: {exception.Message}";
                logLine += Environment.NewLine + $"StackTrace: {exception.StackTrace}";
            }

            logLine += Environment.NewLine;

            lock (_lock)
            {
                File.AppendAllText(GetLogFilePath(), logLine, Encoding.UTF8);
            }
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

                var files = Directory.EnumerateFiles(_logDirectory, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => string.Equals(Path.GetExtension(path), ".log", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase));
                var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTimeUtc < cutoffDate)
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[FileLogger] Unable to delete old log '{file}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FileLogger] Unable to scan log directory '{_logDirectory}': {ex.Message}");
            }
        }
    }
}
