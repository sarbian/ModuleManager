using System;
using ModuleManager.Collections;

namespace ModuleManager.Logging
{
    public class QueueLogger : IBasicLogger
    {
        private readonly IMessageQueue<ILogMessage> queue;

        public QueueLogger(IMessageQueue<ILogMessage> queue)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public void Log(ILogMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            queue.Add(message);
        }
    }
}
