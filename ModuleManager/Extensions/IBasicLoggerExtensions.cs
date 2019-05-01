using System;
using UnityEngine;
using ModuleManager.Logging;

namespace ModuleManager.Extensions
{
    public static class IBasicLoggerExtensions
    {
        public static void Info(this IBasicLogger logger, string message) => logger.Log(new LogMessage(LogType.Log, message));
        public static void Warning(this IBasicLogger logger, string message) => logger.Log(new LogMessage(LogType.Warning, message));
        public static void Error(this IBasicLogger logger, string message) => logger.Log(new LogMessage(LogType.Error, message));

        public static void Exception(this IBasicLogger logger, Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            logger.Log(new LogMessage(LogType.Exception, exception.ToString()));
        }

        public static void Exception(this IBasicLogger logger, string message, Exception exception)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            logger.Log(new LogMessage(LogType.Exception, message + ": " + exception.ToString()));
        }
    }
}
