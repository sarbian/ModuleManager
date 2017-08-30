using System;
using UnityEngine;

namespace ModuleManager.Logging
{
    public class ModLogger : IBasicLogger
    {
        private string prefix;
        private ILogger logger;

        public ModLogger(string prefix, ILogger logger)
        {
            this.prefix = "[" + prefix + "] ";
            this.logger = logger;
        }

        public void Log(LogType logType, string message) => logger.Log(logType, prefix + message);

        public void Info(string message) => Log(LogType.Log, message);
        public void Warning(string message) => Log(LogType.Warning, message);
        public void Error(string message) => Log(LogType.Error, message);
        
        public void Exception(string message, Exception exception)
        {
            Error(message);
            logger.LogException(exception);
        }
    }
}
