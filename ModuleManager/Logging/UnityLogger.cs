using System;
using UnityEngine;

namespace ModuleManager.Logging
{
    public class UnityLogger : IBasicLogger
    {
        private ILogger logger;

        public UnityLogger(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Log(ILogMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            logger.Log(message.LogType, message.Message);
        }
    }
}
