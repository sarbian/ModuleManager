using System;
using UnityEngine;

namespace ModuleManager.Logging
{
    public class ModLogger : IBasicLogger
    {
        private string prefix;
        private IBasicLogger logger;

        public ModLogger(string prefix, IBasicLogger logger)
        {
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentNullException(nameof(prefix));
            this.prefix = "[" + prefix + "] ";
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Log(LogType logType, string message) => logger.Log(logType, prefix + message);
        public void Exception(string message, Exception exception) => logger.Exception(prefix + message, exception);
    }
}
