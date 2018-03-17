using System;
using UnityEngine;
using ModuleManager.Extensions;

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
        
        public void Exception(string message, Exception exception)
        {
            this.Error(message);
            logger.LogException(exception);
        }
    }
}
