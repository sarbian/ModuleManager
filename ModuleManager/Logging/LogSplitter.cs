using System;
using UnityEngine;

namespace ModuleManager.Logging
{
    public class LogSplitter : IBasicLogger
    {
        private readonly IBasicLogger logger1;
        private readonly IBasicLogger logger2;

        public LogSplitter(IBasicLogger logger1, IBasicLogger logger2)
        {
            this.logger1 = logger1 ?? throw new ArgumentNullException(nameof(logger1));
            this.logger2 = logger2 ?? throw new ArgumentNullException(nameof(logger2));
        }

        public void Log(LogType logType, string message)
        {
            logger1.Log(logType, message);
            logger2.Log(logType, message);
        }

        public void Exception(string message, Exception exception)
        {
            logger1.Exception(message, exception);
            logger2.Exception(message, exception);
        }
    }
}
