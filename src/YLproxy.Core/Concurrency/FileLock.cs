using System.Collections.Concurrent;
using System.IO;

namespace YLproxy.Core.Concurrency;

/// <summary>
/// 提供文件级别的读写锁，防止并发访问冲突。
/// 使用 .lock 文件作为互斥信号量，确保跨进程安全。
/// </summary>
public sealed class FileLock : IDisposable
{
    private readonly string _lockFilePath;
    private FileStream? _lockStream;
    private bool _disposed;

    private static readonly ConcurrentDictionary<string, int> _lockCounters = new();
    private static readonly object _globalSync = new();

    /// <summary>
    /// 获取或设置锁获取超时时间（毫秒）。默认 3000ms。
    /// </summary>
    public static int DefaultTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// 获取或设置锁重试间隔（毫秒）。默认 50ms。
    /// </summary>
    public static int RetryIntervalMs { get; set; } = 50;

    /// <summary>
    /// 创建一个文件锁。
    /// </summary>
    /// <param name="filePath">要保护的文件路径。</param>
    /// <param name="timeoutMs">获取锁的超时时间（毫秒）。</param>
    /// <exception cref="TimeoutException">在指定超时内无法获取锁时抛出。</exception>
    public FileLock(string filePath, int timeoutMs = -1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _lockFilePath = filePath + ".lock";
        var effectiveTimeout = timeoutMs > 0 ? timeoutMs : DefaultTimeoutMs;

        // 确保目标目录存在
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(effectiveTimeout);
        var lastException = (Exception?)null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                _lockStream = new FileStream(
                    _lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);

                // 成功获取锁
                lock (_globalSync)
                {
                    _lockCounters.AddOrUpdate(filePath, 1, (_, count) => count + 1);
                }
                return;
            }
            catch (IOException ex)
            {
                lastException = ex;
                // 文件被锁定，等待重试
                Thread.Sleep(RetryIntervalMs);
            }
            catch (UnauthorizedAccessException ex)
            {
                lastException = ex;
                Thread.Sleep(RetryIntervalMs);
            }
        }

        throw new TimeoutException(
            $"Failed to acquire lock for '{filePath}' within {effectiveTimeout}ms.",
            lastException);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _lockStream?.Dispose();
        _lockStream = null;

        lock (_globalSync)
        {
            // 从锁文件路径反推原始文件路径
            var originalPath = _lockFilePath.EndsWith(".lock", StringComparison.Ordinal)
                ? _lockFilePath[..^5]
                : _lockFilePath;

            if (_lockCounters.TryGetValue(originalPath, out var count))
            {
                if (count <= 1)
                {
                    _lockCounters.TryRemove(originalPath, out _);
                    // 清理 .lock 文件
                    try
                    {
                        if (File.Exists(_lockFilePath))
                            File.Delete(_lockFilePath);
                    }
                    catch
                    {
                        // 清理失败不影响主流程
                    }
                }
                else
                {
                    _lockCounters[originalPath] = count - 1;
                }
            }
        }
    }
}
