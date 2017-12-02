using System;
using UnityEngine;

namespace ModuleManager.Logging
{
    public class NormalMessage : ILogMessage
    {
        public readonly LogType logType;
        public readonly string message;

        public NormalMessage(LogType logType, string message)
        {
            this.logType = logType;
            this.message = message;
        }

        public void LogTo(IBasicLogger logger)
        {
            logger.Log(logType, message);
        }
    }
}
