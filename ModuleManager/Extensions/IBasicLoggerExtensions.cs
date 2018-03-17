using System;
using UnityEngine;
using ModuleManager.Logging;

namespace ModuleManager.Extensions
{
    public static class IBasicLoggerExtensions
    {
        public static void Info(this IBasicLogger logger, string message) => logger.Log(LogType.Log, message);
        public static void Warning(this IBasicLogger logger, string message) => logger.Log(LogType.Warning, message);
        public static void Error(this IBasicLogger logger, string message) => logger.Log(LogType.Error, message);
    }
}
