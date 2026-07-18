using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YLproxy.Infrastructure
{
    public static class ExceptionHandler
    {
        /// <summary>
        /// Optional callback for presenting user-facing error messages.
        /// Set by the GUI or API host layer during startup.
        /// </summary>
        public static Action<string, string>? OnUserNotification { get; set; }

        public static void Handle(Exception ex, ILogger logger, string context = "", bool showUser = true, object? data = null)
        {
            logger.Log(LogLevel.Error, $"Exception in {context}: {ex.Message}", ex, data);

            if (showUser && OnUserNotification is not null)
            {
                try
                {
                    OnUserNotification(context, ex.Message);
                }
                catch
                {
                    // Notification failure must not mask the original exception.
                }
            }
        }

        /// <summary>
        /// Wraps a synchronous function with try-catch, logging and returning a default on failure.
        /// </summary>
        /// <param name="data">Optional structured context (e.g. proxy Id) appended to the log entry.</param>
        public static T? TryCatch<T>(Func<T> func, ILogger logger, string context = "", T? defaultValue = default, object? data = null)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                Handle(ex, logger, context, data: data);
                return defaultValue;
            }
        }

        /// <summary>
        /// Wraps a synchronous action with try-catch and logging.
        /// </summary>
        /// <param name="data">Optional structured context (e.g. proxy Id) appended to the log entry.</param>
        public static void TryCatch(Action action, ILogger logger, string context = "", object? data = null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Handle(ex, logger, context, data: data);
            }
        }

        /// <summary>
        /// Wraps an async function with try-catch, logging and returning a default on failure.
        /// </summary>
        /// <param name="data">Optional structured context (e.g. proxy Id) appended to the log entry.</param>
        public static async Task<T?> TryCatchAsync<T>(Func<Task<T>> func, ILogger logger, string context = "", T? defaultValue = default, object? data = null)
        {
            try
            {
                return await func().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Handle(ex, logger, context, data: data);
                return defaultValue;
            }
        }

        /// <summary>
        /// Wraps an async action with try-catch and logging.
        /// </summary>
        /// <param name="data">Optional structured context (e.g. proxy Id) appended to the log entry.</param>
        public static async Task TryCatchAsync(Func<Task> func, ILogger logger, string context = "", object? data = null)
        {
            try
            {
                await func().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Handle(ex, logger, context, data: data);
            }
        }
    }
}
