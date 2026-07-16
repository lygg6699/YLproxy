using System;
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

        public static void Handle(Exception ex, ILogger logger, string context = "", bool showUser = true)
        {
            logger.Error($"Exception in {context}: {ex.Message}", ex);

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

        public static T? TryCatch<T>(Func<T> func, ILogger logger, string context = "", T? defaultValue = default)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                Handle(ex, logger, context);
                return defaultValue;
            }
        }

        public static void TryCatch(Action action, ILogger logger, string context = "")
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Handle(ex, logger, context);
            }
        }

        public static async Task<T?> TryCatchAsync<T>(Func<Task<T>> func, ILogger logger, string context = "", T? defaultValue = default)
        {
            try
            {
                return await func().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Handle(ex, logger, context);
                return defaultValue;
            }
        }

        public static async Task TryCatchAsync(Func<Task> func, ILogger logger, string context = "")
        {
            try
            {
                await func().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Handle(ex, logger, context);
            }
        }
    }
}
