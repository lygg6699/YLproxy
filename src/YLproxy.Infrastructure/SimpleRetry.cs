using System;
using System.Threading;
using System.Threading.Tasks;

namespace YLproxy.Infrastructure
{
    /// <summary>
    /// Simple retry utility for transient operations (file I/O, DB cleanup, etc.).
    /// </summary>
    public static class SimpleRetry
    {
        /// <summary>
        /// Executes an action with a fixed-delay retry policy.
        /// NOTE: This synchronous version uses Thread.Sleep which blocks the calling thread.
        /// If called from a UI thread or a high-throughput async context, prefer the async
        /// overload <see cref="ExecuteAsync(Func{Task}, int, int, ILogger)"/> instead.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="maxAttempts">Maximum number of attempts (default 3).</param>
        /// <param name="delayMs">Delay between attempts in milliseconds (default 50).</param>
        /// <param name="logger">Optional logger for recording retry attempts.</param>
        /// <exception cref="AggregateException">Thrown if all attempts fail, containing all exceptions.</exception>
        public static void Execute(Action action, int maxAttempts = 3, int delayMs = 50, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            if (delayMs < 0) throw new ArgumentOutOfRangeException(nameof(delayMs));

            var exceptions = new System.Collections.Generic.List<Exception>();

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    logger?.Warn($"Retry attempt {attempt + 1}/{maxAttempts} failed: {ex.Message}");

                    if (attempt < maxAttempts - 1)
                        // Synchronous retry uses Thread.Sleep which blocks the calling thread.
                        // For UI thread or high-throughput scenarios, prefer ExecuteAsync to
                        // avoid thread-pool starvation.
                        Thread.Sleep(delayMs);
                }
            }

            throw new AggregateException(
                $"Operation failed after {maxAttempts} attempts.", exceptions);
        }

        /// <summary>
        /// Executes a function with a fixed-delay retry policy.
        /// NOTE: This synchronous version uses Thread.Sleep which blocks the calling thread.
        /// If called from a UI thread or a high-throughput async context, prefer the async
        /// overload <see cref="ExecuteAsync{T}(Func{Task{T}}, int, int, ILogger)"/> instead.
        /// </summary>
        public static T Execute<T>(Func<T> func, int maxAttempts = 3, int delayMs = 50, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(func);
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            if (delayMs < 0) throw new ArgumentOutOfRangeException(nameof(delayMs));

            var exceptions = new System.Collections.Generic.List<Exception>();

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    return func();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    logger?.Warn($"Retry attempt {attempt + 1}/{maxAttempts} failed: {ex.Message}");

                    if (attempt < maxAttempts - 1)
                        // Synchronous retry uses Thread.Sleep which blocks the calling thread.
                        // For UI thread or high-throughput scenarios, prefer ExecuteAsync to
                        // avoid thread-pool starvation.
                        Thread.Sleep(delayMs);
                }
            }

            throw new AggregateException(
                $"Operation failed after {maxAttempts} attempts.", exceptions);
        }

        /// <summary>
        /// Executes an async action with a fixed-delay retry policy.
        /// </summary>
        public static async Task ExecuteAsync(Func<Task> action, int maxAttempts = 3, int delayMs = 50, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            if (delayMs < 0) throw new ArgumentOutOfRangeException(nameof(delayMs));

            var exceptions = new System.Collections.Generic.List<Exception>();

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    await action().ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    logger?.Warn($"Retry attempt {attempt + 1}/{maxAttempts} failed: {ex.Message}");

                    if (attempt < maxAttempts - 1)
                        await Task.Delay(delayMs).ConfigureAwait(false);
                }
            }

            throw new AggregateException(
                $"Operation failed after {maxAttempts} attempts.", exceptions);
        }

        /// <summary>
        /// Executes an async function with a fixed-delay retry policy.
        /// </summary>
        public static async Task<T> ExecuteAsync<T>(Func<Task<T>> func, int maxAttempts = 3, int delayMs = 50, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(func);
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            if (delayMs < 0) throw new ArgumentOutOfRangeException(nameof(delayMs));

            var exceptions = new System.Collections.Generic.List<Exception>();

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    return await func().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    logger?.Warn($"Retry attempt {attempt + 1}/{maxAttempts} failed: {ex.Message}");

                    if (attempt < maxAttempts - 1)
                        await Task.Delay(delayMs).ConfigureAwait(false);
                }
            }

            throw new AggregateException(
                $"Operation failed after {maxAttempts} attempts.", exceptions);
        }
    }
}
