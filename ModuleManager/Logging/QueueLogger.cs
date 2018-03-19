using System;
using UnityEngine;
using ModuleManager.Collections;

namespace ModuleManager.Logging
{
    public class QueueLogger : IBasicLogger
    {
        private readonly IMessageQueue<ILogMessage> queue;

        public QueueLogger(IMessageQueue<ILogMessage> queue)
        {
            this.queue = queue;
        }

        public void Log(LogType logType, string message) => queue.Add(new NormalMessage(logType, message));
        public void Exception(string message, Exception exception) => queue.Add(new ExceptionMessage(message, exception));
    }
}
