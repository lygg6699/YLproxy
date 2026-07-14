using System;

namespace YLproxy.Infrastructure
{
    public static class ExceptionHandler
    {
        public static void Handle(Exception ex, ILogger logger, string context = "", bool showUser = true)
        {
            logger.Error($"Exception in {context}: {ex.Message}", ex);

            // In a real implementation, this would use a UI service to show message
            // For now, we'll just log it - UI notification would come later
            // if (showUser)
            // {
            //     // TODO: Show user-friendly error message
            // }
        }

        public static T TryCatch<T>(Func<T> func, ILogger logger, string context = "", T defaultValue = default)
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
    }
}