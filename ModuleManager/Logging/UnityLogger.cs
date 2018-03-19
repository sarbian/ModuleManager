using System;
using UnityEngine;
using ModuleManager.Extensions;

namespace ModuleManager.Logging
{
    public class UnityLogger : IBasicLogger
    {
        private ILogger logger;

        public UnityLogger(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Log(LogType logType, string message) => logger.Log(logType, message);

        public void Exception(string message, Exception exception)
        {
            this.Error(message);
            logger.LogException(exception);
        }
    }
}
